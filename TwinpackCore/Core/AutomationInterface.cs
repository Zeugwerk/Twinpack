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
        /// Returns the version as TwinCAT spells it, given the version a NuGet feed reports. NuGet keeps
        /// all four parts of a version, so there is nothing to undo and the version is used as it is.
        /// Packages published under the superseded convention that moved the revision into the
        /// prerelease are still understood: <c>1.0.0-1</c> and <c>1.0.0-feat-ci.1</c> both read as
        /// <c>1.0.0.1</c>.
        /// <para>
        /// A three part version is deliberately left alone rather than padded to <c>x.y.z.0</c>: TwinCAT
        /// accepts three part library versions and registers them under exactly that string, so padding
        /// would produce a version no installed library can be matched against. That a library versioned
        /// <c>x.y.z.0</c> is published as <c>x.y.z</c> and therefore indistinguishable from one versioned
        /// <c>x.y.z</c> is resolved where it can be, against the versions actually installed, not here.
        /// </para>
        /// </summary>
        public static string FourPartVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return version;

            var dash = version.IndexOf('-');
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

        /// <summary>
        /// Inverse of <see cref="FourPartVersion"/>: the NuGet version a TwinCAT
        /// <c>x.y.z.w[-qualifier]</c> library is published under. A four part version is one NuGet
        /// understands directly, so it is used as it is and only a leading <c>v</c> is dropped.
        /// Anything that is not four numbers is returned unchanged.
        /// <para>
        /// NuGet normalizes a <c>w</c> of 0 away when it writes the nuspec, so <c>1.0.0.0</c> is
        /// published as <c>1.0.0</c> and <c>1.0.0.0-feat-ci</c> as <c>1.0.0-feat-ci</c>. That is
        /// lossless: the two spellings are the same version to NuGet, so a lookup for either finds
        /// the package, and <see cref="FourPartVersion"/> pads the 0 back for the TwinCAT side.
        /// </para>
        /// </summary>
        public static string NugetVersion(string version)
        {
            var v = version?.Trim().TrimStart(new char[] { 'v', 'V', ' ', '\t' });
            if (string.IsNullOrEmpty(v))
                return version;

            var dash = v.IndexOf('-');
            var prefix = dash < 0 ? v : v.Substring(0, dash);

            var parts = prefix.Split('.');
            if (parts.Length != 4 || !parts.All(x => int.TryParse(x, out _)))
                return version;

            return v;
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

        /// <summary>
        /// Whether two TwinCAT library versions name the same version, counting a missing fourth part as
        /// 0 so that <c>1.2.3</c> and <c>1.2.3.0</c> compare equal. Both spellings reach us for the same
        /// library: NuGet publishes a library versioned <c>1.2.3.0</c> as <c>1.2.3</c>, while TwinCAT also
        /// allows a library to be versioned <c>1.2.3</c> outright. Versions that cannot be parsed at all
        /// are not equivalent rather than an error, because this is used to search installed libraries.
        /// </summary>
        public static bool TwinCATLibraryVersionsEquivalent(string left, string right)
        {
            try
            {
                if (TwinCATLibraryVersionsEqual(left, right))
                    return true;

                var l = TwinCATLibraryVersion(left);
                var r = TwinCATLibraryVersion(right);
                if (string.IsNullOrEmpty(l) || string.IsNullOrEmpty(r) || l == "*" || r == "*")
                    return false;

                return Version.TryParse(l, out var lv) && Version.TryParse(r, out var rv)
                    && PadToFourParts(lv) == PadToFourParts(rv);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static Version PadToFourParts(Version version)
        {
            return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
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