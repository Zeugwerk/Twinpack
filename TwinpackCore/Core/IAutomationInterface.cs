using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Models;

namespace Twinpack.Core
{
    public interface IAutomationInterface
    {
        event EventHandler<EventArgs> ProgressedEvent;
        public bool IsSupported(string tcversion);
        public string DefaultLibraryCachePath { get; }
        public string TwincatPath { get; }
        public string LicensesPath { get; }
        public string BootFolderPath { get; }
        public string SolutionPath { get; }
        public Task<string> ResolveEffectiveVersionAsync(string projectName, string plcName, string placeholderName);
        public Task SetPackageVersionAsync(ConfigPlcProject package, CancellationToken cancellationToken = default);
        public Task<bool> IsPackageInstalledAsync(PackageItem package);
        public bool IsPackageInstalled(PackageItem package);
        public Task AddPackageAsync(PackageItem package);
        public Task RemovePackageAsync(PackageItem package, bool uninstall = false, bool forceRemoval = false);
        public Task RemoveAllPackagesAsync(string projectName, string plcName);
        public Task InstallPackageAsync(PackageItem package, string cachePath = null);
        public Task<bool> UninstallPackageAsync(PackageItem package);
        public Task CloseAllPackageRelatedWindowsAsync(List<PackageItem> packages);
        public Task SaveAllAsync();
    }
}