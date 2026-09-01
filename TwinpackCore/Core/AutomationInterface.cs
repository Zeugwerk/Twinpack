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
            version = version?.Trim().TrimStart(new char[] { 'v', 'V', ' ', '\t' }).Replace('-', '.');
            if (version != null && !Version.TryParse(version, out _))
                throw new ArgumentException("Version has wrong format! Valid formats include '1.0.0.0', 'v1.0.0.0', '1.0.0-0'");

            return version;
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