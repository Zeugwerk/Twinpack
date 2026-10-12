using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if !TWINPACK_HEADLESS
using Microsoft.Win32;
#endif

namespace Twinpack.Core
{
    /// <summary>
    /// Resolves TwinCAT install roots and OEM license folders across 4024 and 4026 layouts.
    /// Concatenating a missing BootDir/TWINCAT3DIR with \CustomConfig\Licenses previously
    /// created C:\CustomConfig, which TwinCAT never reads.
    /// </summary>
    public static class TwincatInstall
    {
        static readonly string[] RegistryBootDirKeys =
        {
            @"Software\Wow6432Node\Beckhoff\TwinCAT3\3.1",
            @"Software\Beckhoff\TwinCAT3\3.1"
        };

        /// <summary>
        /// Writable TwinCAT roots that actually host CustomConfig\Licenses.
        /// Program Files is the binary install tree: TwinCAT does not read OEM
        /// .tmc files from there, and creating CustomConfig is denied without elevation.
        /// </summary>
        static readonly string[] WellKnownRoots =
        {
            @"C:\ProgramData\Beckhoff\TwinCAT\3.1",
            @"C:\TwinCAT\3.1"
        };

        static readonly string LicenseFolderSuffix = @"CustomConfig\Licenses";

        public static IReadOnlyList<string> DiscoverRoots()
        {
            var roots = new List<string>();

            foreach (var bootDir in ReadRegistryBootDirs())
                TryAddRoot(roots, RootFromBootDir(bootDir));

            TryAddRoot(roots, Environment.GetEnvironmentVariable("TWINCAT3DIR"));

            foreach (var wellKnown in WellKnownRoots)
                TryAddRoot(roots, wellKnown);

            return roots;
        }

        /// <summary>
        /// Folders that receive <c>.tml</c>/<c>.tmx</c>/<c>.tme</c> module libraries.
        /// 4026 publishes <c>RepositoryDir</c> as <c>C:\ProgramData\Beckhoff\TwinCAT\3.1\Repository</c>.
        /// 4024 keeps them in <c>C:\TwinCAT\3.1\Repository</c>. <c>TWINCAT3DIR</c> is the Program Files
        /// install tree and is not a repository.
        /// </summary>
        public static IReadOnlyList<string> DiscoverRepositoryFolders()
        {
            var roots = new List<string>();
            foreach (var bootDir in ReadRegistryBootDirs())
                TryAddRoot(roots, RootFromBootDir(bootDir));
            foreach (var wellKnown in WellKnownRoots)
                TryAddRoot(roots, wellKnown);

            return RepositoryFolders(roots, ReadRegistryRepositoryDirs());
        }

        public static IReadOnlyList<string> RepositoryFolders(IEnumerable<string> roots, IEnumerable<string> registryDirs)
        {
            var folders = new List<string>();

            if (registryDirs != null)
            {
                foreach (var dir in registryDirs)
                    TryAddRepositoryFolder(folders, dir);
            }

            if (roots != null)
            {
                foreach (var root in roots)
                {
                    if (!IsUsableRoot(root))
                        continue;

                    TryAddRepositoryFolder(folders, Path.Combine(root, "Repository"));
                }
            }

            return folders;
        }

        public static IReadOnlyList<string> DiscoverLicenseFolders()
        {
            var roots = new List<string>();

            foreach (var bootDir in ReadRegistryBootDirs())
                TryAddRoot(roots, RootFromBootDir(bootDir));

            foreach (var wellKnown in WellKnownRoots)
                TryAddRoot(roots, wellKnown);

            return LicenseFolders(roots);
        }

        public static IReadOnlyList<string> LicenseFolders(IEnumerable<string> roots)
        {
            var folders = new List<string>();
            if (roots == null)
                return folders;

            foreach (var root in roots)
            {
                if (!IsUsableRoot(root))
                    continue;

                TryAddLicenseFolder(folders, Path.Combine(root, LicenseFolderSuffix));
            }

            return folders;
        }

        public static IReadOnlyList<string> ResolveLicenseFolders(string licensesPath, IEnumerable<string> licensesPaths)
        {
            var candidates = new List<string>();
            if (licensesPaths != null)
                candidates.AddRange(licensesPaths);
            if (!string.IsNullOrWhiteSpace(licensesPath))
                candidates.Add(licensesPath);
            candidates.AddRange(DiscoverLicenseFolders());

            var folders = new List<string>();
            foreach (var folder in candidates)
                TryAddLicenseFolder(folders, folder);
            return folders;
        }

        public static string RootFromBootDir(string bootDir)
        {
            if (string.IsNullOrWhiteSpace(bootDir))
                return null;

            try
            {
                return new DirectoryInfo(bootDir.Trim()).Parent?.FullName;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsUsableRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return false;

            try
            {
                var trimmed = root.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (trimmed.Length <= 2 && trimmed.EndsWith(":"))
                    return false;

                var full = Path.GetFullPath(root.Trim());
                var drive = Path.GetPathRoot(full);
                if (string.IsNullOrEmpty(drive))
                    return false;

                if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), drive.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    return false;

                if (Directory.GetParent(full) == null)
                    return false;

                return Directory.Exists(full);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsUsableLicenseFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return false;

            try
            {
                var full = Path.GetFullPath(folder.Trim());
                var marker = $"{Path.DirectorySeparatorChar}CustomConfig{Path.DirectorySeparatorChar}";
                var index = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                var drive = Path.GetPathRoot(full);
                if (string.IsNullOrEmpty(drive) || index <= drive.Length)
                    return false;

                return IsUsableRoot(full.Substring(0, index));
            }
            catch
            {
                return false;
            }
        }

        static void TryAddRoot(List<string> roots, string root)
        {
            if (!IsUsableRoot(root))
                return;

            var full = Path.GetFullPath(root.Trim());
            if (roots.Any(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                return;

            roots.Add(full);
        }

        static void TryAddLicenseFolder(List<string> folders, string folder)
        {
            if (!IsUsableLicenseFolder(folder))
                return;

            var full = Path.GetFullPath(folder.Trim());
            if (folders.Any(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                return;

            folders.Add(full);
        }

        static IEnumerable<string> ReadRegistryBootDirs()
        {
            return ReadRegistryValues("BootDir");
        }

        static IEnumerable<string> ReadRegistryRepositoryDirs()
        {
            return ReadRegistryValues("RepositoryDir");
        }

        static IEnumerable<string> ReadRegistryValues(string valueName)
        {
#if TWINPACK_HEADLESS
            yield break;
#else
            foreach (var keyPath in RegistryBootDirKeys)
            {
                string value = null;
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                        value = key?.GetValue(valueName) as string;
                }
                catch
                {
                }

                if (!string.IsNullOrWhiteSpace(value))
                    yield return value;
            }
#endif
        }

        static bool IsUnderProgramFiles(string path)
        {
            var markers = new[]
            {
                $"{Path.DirectorySeparatorChar}Program Files{Path.DirectorySeparatorChar}",
                $"{Path.DirectorySeparatorChar}Program Files (x86){Path.DirectorySeparatorChar}"
            };
            return markers.Any(marker => path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void TryAddRepositoryFolder(List<string> folders, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;

            string full;
            try
            {
                full = Path.GetFullPath(folder.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return;
            }

            // Program Files is the product install. Writing a Repository folder there is denied
            // and TwinCAT does not load module libraries from it.
            if (IsUnderProgramFiles(full))
                return;

            if (!IsUsableRoot(Path.GetDirectoryName(full)))
                return;

            if (folders.Any(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                return;

            folders.Add(full);
        }
    }
}
