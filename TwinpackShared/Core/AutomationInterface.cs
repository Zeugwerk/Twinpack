using EnvDTE;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Models;
using Twinpack.Models.Api;

namespace Twinpack.Core
{
    public class AutomationInterface : IAutomationInterface
    {
        public event EventHandler<ProgressEventArgs> ProgressedEvent = delegate { };

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static readonly Guid _libraryManagerGuid = Guid.Parse("e1825adc-a79c-4e8e-8793-08d62d84be5b");

        VisualStudio _visualStudio;
        SynchronizationContext _synchronizationContext;

        public AutomationInterface(VisualStudio visualStudio)
        {
            _visualStudio = visualStudio;
            _synchronizationContext = SynchronizationContext.Current;
        }

        private async Task SwitchToMainThreadAsync(CancellationToken cancellationToken=default)
        {
            await _synchronizationContext;
        }
        public string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        public string TwincatPath
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Beckhoff\\TwinCAT3\\3.1"))
                    {
                        var bootDir = key?.GetValue("BootDir") as string;

                        // need to do GetParent twice because of the trailing \
                        return bootDir == null ? null : new DirectoryInfo(bootDir)?.Parent?.FullName;
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        public string LicensesPath { get => TwincatPath + @"\CustomConfig\Licenses"; }
        public string BootFolderPath { get => TwincatPath + @"\Boot"; }
        public string SolutionPath { get => Path.GetDirectoryName(_visualStudio.Dte.Solution.FullName); }
        public bool IsSupported(string tcversion)
        {
            return true;
        }

        public string ResolveEffectiveVersion(string projectName, string placeholderName)
        {
            ResolvePlaceholder(projectName, placeholderName, out _, out string effectiveVersion);

            return effectiveVersion;
        }

        private ITcPlcLibrary ResolvePlaceholder(string projectName, string placeholderName, out string distributorName, out string effectiveVersion)
        {
            ITcPlcLibraryManager libManager = LibraryManager(projectName);
            return ResolvePlaceholder(libManager, placeholderName, out distributorName, out effectiveVersion);
        }

        private ITcPlcLibrary ResolvePlaceholder(ITcPlcLibraryManager libManager, string placeholderName, out string distributorName, out string effectiveVersion)
        {
            // Try to remove the already existing reference
            foreach (ITcPlcLibRef item in libManager.References)
            {
                string itemPlaceholderName;
                ITcPlcLibrary plcLibrary;

                try
                {
                    ITcPlcPlaceholderRef2 plcPlaceholder; // this will throw if the library is currently not installed
                    plcPlaceholder = (ITcPlcPlaceholderRef2)item;

                    itemPlaceholderName = plcPlaceholder.PlaceholderName;

                    if (plcPlaceholder.EffectiveResolution != null)
                        plcLibrary = plcPlaceholder.EffectiveResolution;
                    else
                        plcLibrary = plcPlaceholder.DefaultResolution;

                    effectiveVersion = plcLibrary.Version;
                    distributorName = plcLibrary.Distributor;
                }
                catch
                {
                    plcLibrary = (ITcPlcLibrary)item;
                    effectiveVersion = null;
                    itemPlaceholderName = plcLibrary.Name.Split(',')[0];
                    distributorName = plcLibrary.Distributor;
                }

                if (string.Equals(itemPlaceholderName, placeholderName, StringComparison.InvariantCultureIgnoreCase))
                    return plcLibrary;
            }

            distributorName = null;
            effectiveVersion = null;
            return null;
        }

        private ITcSysManager SystemManager(string projectName = null)
        {
            var ready = false;
            while (!ready)
            {
                ready = true;
                foreach (EnvDTE.Project project in _visualStudio.Dte.Solution.Projects)
                {
                    if (project == null)
                        ready = false;
                    else if ((projectName == null || project?.Name == projectName) && project.Object as ITcSysManager != null)
                        return project.Object as ITcSysManager;
                }

                if (!ready)
                    System.Threading.Thread.Sleep(1000);
            }

            return null;
        }

        private ITcPlcLibraryManager LibraryManager(string projectName = null)
        {
            var systemManager = SystemManager(projectName);
            
            if (projectName == null)
            {
                var plcConfiguration = systemManager.LookupTreeItem("TIPC");
                for (var j = 1; j <= plcConfiguration.ChildCount; j++)
                {
                    var plc = (plcConfiguration.Child[j] as ITcProjectRoot)?.NestedProject;
                    for (var k = 1; k <= (plc?.ChildCount ?? 0); k++)
                    {
                        if (plc.Child[k] as ITcPlcLibraryManager != null)
                        {
                            return plc.Child[k] as ITcPlcLibraryManager;
                        }
                    }
                }
            }
            else
            {
                var projectRoot = systemManager.LookupTreeItem($"TIPC^{projectName}");
                var plc = (projectRoot as ITcProjectRoot)?.NestedProject;
                for (var k = 1; k <= (plc?.ChildCount ?? 0); k++)
                {
                    if (plc.Child[k] as ITcPlcLibraryManager != null)
                    {
                        return plc.Child[k] as ITcPlcLibraryManager;
                    }
                }

            }

            return null;
        }

        public string GuessDistributorName(ITcPlcLibraryManager libManager, string libraryName, string version)
        {
            // try to find the vendor
            foreach (ITcPlcLibrary r in libManager.ScanLibraries())
            {
                if (r.Name == libraryName && (r.Version == version || version == "*" || version == null))
                {
                    return r.Distributor;
                }
            }

            return null;
        }

        public async Task<bool> IsPackageInstalledAsync(CatalogItem package)
        {
            await SwitchToMainThreadAsync();
            return IsPackageInstalled(package);
        }

        private bool IsPackageInstalled(CatalogItem package)
        {
            var libraryManager = LibraryManager(package.PlcName);
            bool referenceFound = false;

            if (libraryManager != null)
            {
                foreach (ITcPlcLibrary r in libraryManager.ScanLibraries())
                {
                    if (string.Equals(r.Name, package.PackageVersion.Title, StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(r.Distributor, package.PackageVersion.DistributorName, StringComparison.InvariantCultureIgnoreCase) &&
                        (r.Version == package.Config.Version || package.Config.Version == null))
                    {
                        referenceFound = true;
                        break;
                    }
                }

                if (referenceFound)
                {
                    _logger.Info($"{package.PackageVersion.Title} {package.PackageVersion.Version} (distributor: {package.PackageVersion.DistributorName}), it already exists on the system");
                }
            }

            return referenceFound;
        }

        private void CloseAllPackageRelatedWindows(CatalogItem package)
        {
            _visualStudio.Dte.ExecuteCommand("File.SaveAll");

            // Close all windows that have been opened with a library manager
            foreach (EnvDTE.Window window in _visualStudio.Dte.Windows)
            {
                try
                {
                    var obj = (window as dynamic).Object;
                    if (obj == null)
                        continue;

                    if (obj.EditorViewFactoryGuid == _libraryManagerGuid)
                    {
                        window.Close(vsSaveChanges.vsSaveChangesNo);
                        continue;
                    }

                    if (!(obj.FileName as string).StartsWith("["))
                        continue;

                    if (Regex.Match(obj.FileName, $@"\[{package.PackageVersion.Name}.*?\({package.PackageVersion.DistributorName}\)\].*", RegexOptions.IgnoreCase).Success)
                    {
                        window.Close(vsSaveChanges.vsSaveChangesNo);
                        continue;
                    }

                    foreach (var dependency in package.PackageVersion.Dependencies ?? new List<PackageVersionGetResponse>())
                    {
                        if (Regex.Match(obj.FileName, $@"\[{dependency.Name}.*?\({dependency.DistributorName}\)\].*", RegexOptions.IgnoreCase).Success)
                        {
                            window.Close(vsSaveChanges.vsSaveChangesNo);
                            break;
                        }
                    }

                }
                catch { }
            }
        }

        public async Task AddPackageAsync(CatalogItem package)
        {
            await SwitchToMainThreadAsync();

            // add actual packages
            var libraryManager = LibraryManager(package.PlcName);
            await AddPackageAsync(libraryManager, package);
        }

        private async Task AddPackageAsync(ITcPlcLibraryManager libraryManager, CatalogItem package)
        {
            await SwitchToMainThreadAsync();

            var options = package.Config.Options;
            var libraryName = package.PackageVersion.Title;
            var version = package.PackageVersion.Version;
            var distributorName = package.PackageVersion.DistributorName ?? GuessDistributorName(libraryManager, libraryName, version);

            // if we can't find the reference with the distributor name from the package, fallback to looking it up
            if (!await IsPackageInstalledAsync(package))
                distributorName = GuessDistributorName(libraryManager, libraryName, version);

            await RemovePackageAsync(package);

            _logger.Info($"Adding {package.PackageVersion.Name} {version} (distributor: {distributorName})");
            if (options?.LibraryReference == true)
                libraryManager.AddLibrary(libraryName, version, distributorName);
            else
                libraryManager.AddPlaceholder(libraryName, libraryName, version, distributorName);

            if (options != null)
            {
                var referenceItem = (libraryManager as ITcSmTreeItem).LookupChild(libraryName);
                var referenceXml = referenceItem.ProduceXml(bRecursive: true);
                var referenceDoc = XDocument.Parse(referenceXml);

                if (options?.QualifiedOnly == true)
                {
                    var qualifiedOnlyItem = referenceDoc.Elements("TreeItem")
                        .Elements("VSProperties")
                        .Elements("VSProperty")
                        .Where(x => x.Element("Name").Value == "QualifiedlAccessOnly")
                        .Elements("Value").FirstOrDefault();
                    qualifiedOnlyItem.Value = options?.QualifiedOnly == true ? "True" : "False";
                }

                if (options?.HideWhenReferencedAsDependency == true)
                {
                    var hideReferenceItem = referenceDoc.Elements("TreeItem")
                    .Elements("VSProperties")
                    .Elements("VSProperty")
                    .Where(x => x.Element("Name").Value == "HideReference")
                    .Elements("Value").FirstOrDefault();
                    hideReferenceItem.Value = options?.HideWhenReferencedAsDependency == true ? "True" : "False";
                }

                if (options?.Optional == true)
                {
                    var optionalItem = referenceDoc.Elements("TreeItem")
                        .Elements("VSProperties")
                        .Elements("VSProperty")
                        .Where(x => x.Element("Name").Value == "Optional")
                        .Elements("Value").FirstOrDefault();
                    optionalItem.Value = options?.Optional == true ? "True" : "False";
                }

                if (options?.PublishSymbolsInContainer == true)
                {
                    var publishSymbolsInContainerItem = referenceDoc.Elements("TreeItem")
                    .Elements("VSProperties")
                    .Elements("VSProperty")
                    .Where(x => x.Element("Name").Value == "PublishAll")
                    .Elements("Value").FirstOrDefault();
                    publishSymbolsInContainerItem.Value = options?.PublishSymbolsInContainer == true ? "True" : "False";
                }

                referenceItem.ConsumeXml(referenceDoc.ToString());
            }
        }

        public async Task RemovePackageAsync(CatalogItem package, bool uninstall=false)
        {
            await SwitchToMainThreadAsync();

            var libraryManager = LibraryManager(package.PlcName);
            CloseAllPackageRelatedWindows(package);

            var plcLibrary = ResolvePlaceholder(package.PlcName, package.PackageVersion.Title, out _, out _);
            if (plcLibrary != null)
                libraryManager.RemoveReference(package.PackageVersion.Title);

            if (uninstall)
            {
                _logger.Info($"Uninstalling package {package.PackageVersion.Name} from system ...");
                await UninstallPackageAsync(package);
            }
        }

        public async Task InstallPackageAsync(CatalogItem package, string cachePath = null)
        {
            await SwitchToMainThreadAsync();

            var libraryManager = LibraryManager(package.PlcName);
            var packageVersion = package.PackageVersion;

            _logger.Info($"Installing {package.PackageVersion.Name} {package.PackageVersion.Version}");

            var suffix = package.PackageVersion.Compiled == 1 ? "compiled-library" : "library";
            libraryManager.InstallLibrary("System", Path.GetFullPath($@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}\{packageVersion.Name}_{packageVersion.Version}.{suffix}"), bOverwrite: true);
        }

        public async Task UninstallPackageAsync(CatalogItem package)
        {
            await SwitchToMainThreadAsync();

            var libraryManager = LibraryManager(package.PlcName);

            libraryManager.UninstallLibrary("System", package.PackageVersion.Title, package.PackageVersion.Version, package.PackageVersion.DistributorName);
        }
    }
}