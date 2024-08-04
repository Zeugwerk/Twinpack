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
        public string ResolveEffectiveVersion(string projectName, string placeholderName);
        public Task<bool> IsPackageInstalledAsync(CatalogItem package);
        public Task AddPackageAsync(CatalogItem package);
        public Task RemovePackageAsync(CatalogItem package, bool uninstall = false);
        public Task InstallPackageAsync(CatalogItem package, string cachePath = null);
        public Task UninstallPackageAsync(CatalogItem package);
    }
}