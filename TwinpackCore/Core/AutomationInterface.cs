using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Models;

namespace Twinpack.Core
{
    public abstract class AutomationInterface : IAutomationInterface
    {
        public string DefaultLibraryCachePath { get { return Path.Combine(Directory.GetCurrentDirectory(), ".Zeugwerk", "libraries"); } }

        public string TwincatPath { get => TwincatPaths.FirstOrDefault(); }
        public IReadOnlyList<string> TwincatPaths { get => TwincatInstall.DiscoverRoots(); }
        public string LicensesPath { get => LicensesPaths.FirstOrDefault(); }
        public IReadOnlyList<string> LicensesPaths { get => TwincatInstall.DiscoverLicenseFolders(); }
        public string BootFolderPath { get => TwincatPath == null ? null : Path.Combine(TwincatPath, "Boot"); }

        public bool IsSupported(string tcversion)
        {
            var split = tcversion?.Replace("TC", "").Split('.').Select(x => int.Parse(x)).ToArray();
            var v = new Version(split[0], split[1], split[2], split[3]);
            return (MinVersion == null || v >= MinVersion) && (MaxVersion == null || v <= MaxVersion);
        }

        public static string NormalizedVersion(string version)
        {
            version = version?.Trim().TrimStart(new char[] { 'v', 'V', ' ', '\t' });
            if (version != null && !Version.TryParse(TwincatNumericVersion(version), out _))
                throw new ArgumentException("Version has wrong format! Valid formats include '1.0.0.0', 'v1.0.0.0', '1.0.0-0', '1.0.0.0-feat-ci'");

            var dash = version?.IndexOf('-') ?? -1;
            if (dash >= 0 && int.TryParse(version.Substring(dash + 1), out _))
                return TwincatNumericVersion(version);

            return version;
        }

        /// <summary>
        /// TwinCAT plcproj versions are four integers. Package versions may keep a SemVer
        /// prerelease (<c>1.0.0.0-feat-ci</c>). The old <c>1.0.0-0</c> form still maps to <c>1.0.0.0</c>.
        /// </summary>
        public static string TwincatNumericVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return version;

            var dash = version.IndexOf('-');
            if (dash < 0)
                return version;

            var prefix = version.Substring(0, dash);
            var suffix = version.Substring(dash + 1);
            if (int.TryParse(suffix, out _))
                return $"{prefix}.{suffix}";

            var revision = PrereleaseRevision(prefix, suffix);
            if (revision != null)
                return $"{prefix}.{revision}";

            return prefix;
        }

        /// <summary>
        /// NuGet packages of TwinCAT libraries carry the 4th version part in the prerelease,
        /// <c>1.0.0-1</c> is <c>1.0.0.1</c> and <c>1.0.0-feat-ci.1</c> is <c>1.0.0.1-feat-ci</c>.
        /// Returns the version as <c>x.y.z.w[-qualifier]</c>, or unchanged if it has another form.
        /// </summary>
        public static string FourPartVersion(string version)
        {
            var dash = version?.IndexOf('-') ?? -1;
            if (dash < 0)
                return version;

            var prefix = version.Substring(0, dash);
            var suffix = version.Substring(dash + 1);
            if (prefix.Split('.').Length != 3)
                return version;

            if (int.TryParse(suffix, out _))
                return $"{prefix}.{suffix}";

            var revision = PrereleaseRevision(prefix, suffix);
            if (revision != null)
                return $"{prefix}.{revision}-{suffix.Substring(0, suffix.Length - revision.Length - 1)}";

            return version;
        }

        private static string PrereleaseRevision(string prefix, string suffix)
        {
            var dot = suffix.LastIndexOf('.');
            if (prefix.Split('.').Length != 3 || dot <= 0 || !int.TryParse(suffix.Substring(dot + 1), out _))
                return null;

            return suffix.Substring(dot + 1);
        }

        /// <summary>
        /// Version string TwinCAT library manager / plcproj placeholders accept.
        /// Keeps <c>*</c> and empty values; otherwise strips a SemVer qualifier.
        /// </summary>
        public static string TwinCATLibraryVersion(string version)
        {
            if (string.IsNullOrEmpty(version) || version == "*")
                return version;

            return TwincatNumericVersion(NormalizedVersion(version));
        }

        public static bool TwinCATLibraryVersionsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || left == "*" || string.IsNullOrEmpty(right) || right == "*")
                return string.IsNullOrEmpty(left) || left == "*" || string.IsNullOrEmpty(right) || right == "*";

            if (string.Equals(left, right, StringComparison.Ordinal))
                return true;

            return string.Equals(TwinCATLibraryVersion(left), TwinCATLibraryVersion(right), StringComparison.Ordinal);
        }

        public abstract string SolutionPath { get; }
        public abstract Task<string> ResolveEffectiveVersionAsync(string projectName, string plcName, string placeholderName);
        public abstract Task SetPackageVersionAsync(ConfigPlcProject plc, CancellationToken cancellationToken = default);
        public abstract Task<bool> IsPackageInstalledAsync(PackageItem package);
        public abstract bool IsPackageInstalled(PackageItem package);
        public abstract Task AddPackageAsync(PackageItem package);
        public abstract Task RemovePackageAsync(PackageItem package, bool uninstall = false, bool forceRemoval = false);
        public abstract Task RemoveAllPackagesAsync(string projectName, string plcName);
        public abstract Task InstallPackageAsync(PackageItem package, string cachePath = null);
        public abstract Task<bool> UninstallPackageAsync(PackageItem package);
        public abstract Task CloseAllPackageRelatedWindowsAsync(List<PackageItem> packages);
        public abstract Task SaveAllAsync();
        protected abstract Version MinVersion { get; }
        protected abstract Version MaxVersion { get; }
        public abstract void SaveAsLibrary(ConfigPlcProject plc, string filePath);
    }
}