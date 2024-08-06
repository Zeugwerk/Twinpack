using System;
using System.Management;
using System.Threading.Tasks;
using Twinpack.Models;

namespace Twinpack.Core
{
    public interface IAutomationInterface
    {
        event EventHandler<ProgressEventArgs> ProgressedEvent;
        public bool IsSupported(string tcversion);
        public string DefaultLibraryCachePath { get; }
        public string TwincatPath { get; }
        public string LicensesPath { get; }
        public string BootFolderPath { get; }
        public string SolutionPath { get; }
        public string ResolveEffectiveVersion(string projectName, string plcName, string placeholderName);
        public Task<bool> IsPackageInstalledAsync(PackageItem package);
        public Task AddPackageAsync(PackageItem package);
        public Task RemovePackageAsync(PackageItem package, bool uninstall = false);
        public Task InstallPackageAsync(PackageItem package, string cachePath = null);
        public Task UninstallPackageAsync(PackageItem package);
        public void SaveAll();
    }
}