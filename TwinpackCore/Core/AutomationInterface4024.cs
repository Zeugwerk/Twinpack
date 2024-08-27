using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using NLog;
using NuGet.Protocol;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Configuration;
using Twinpack.Exceptions;
using Twinpack.Models;

namespace Twinpack.Core
{
    public class AutomationInterface4024 : AutomationInterface, IAutomationInterface
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        protected static readonly Guid _libraryManagerGuid = Guid.Parse("e1825adc-a79c-4e8e-8793-08d62d84be5b");

        VisualStudio _visualStudio;
        protected SynchronizationContext _synchronizationContext;

        public AutomationInterface4024(VisualStudio visualStudio)
        {
            _visualStudio = visualStudio;
            _synchronizationContext = SynchronizationContext.Current;
        }

        private List<PlcLibrary> _referenceCache = new List<PlcLibrary>();
        private Dictionary<Tuple<string, string, string>, string> _effectiveVersionCache = new Dictionary<Tuple<string, string, string>, string>();

        protected override Version MinVersion => new Version(3, 1, 4024, 0);
        protected override Version MaxVersion => null;

        public override string SolutionPath { get => Path.GetFullPath(Path.GetDirectoryName(_visualStudio.Solution.FullName)); }

        public override async Task SaveAllAsync()
        {
            await _synchronizationContext;
            _visualStudio?.SaveAll();
        }

        public override async Task<string> ResolveEffectiveVersionAsync(string projectName, string plcName, string placeholderName)
        {
            var key = new Tuple<string, string, string>(projectName, plcName, placeholderName);
            if (_effectiveVersionCache.ContainsKey(key))
                return _effectiveVersionCache[key];

            await _synchronizationContext;

            ITcPlcLibraryManager libManager = LibraryManager(projectName, plcName);
            ResolvePlaceholder(libManager, placeholderName, out _, out var effectiveVersion);

            _effectiveVersionCache[key] = effectiveVersion;
            return effectiveVersion;
        }

        protected ITcPlcLibrary ResolvePlaceholder(ITcPlcLibraryManager libManager, string placeholderName, out string distributorName, out string effectiveVersion)
        {
            if (_synchronizationContext != SynchronizationContext.Current)
                throw new Exception("Invalid synchronization context!");

            // getter references might throw (Starting from TC3.1.4024.35)
            ITcPlcReferences references;
            try
            {
                references = libManager.References;
            }
            catch (FormatException ex) // TwinCAT throws a FormatException for some reason
            {
                _logger.Warn("PLC contains packages not available on the system, please update or remove them");
                _logger.Trace(ex);
                distributorName = null;
                effectiveVersion = null;
                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                _logger.Trace(ex);
                distributorName = null;
                effectiveVersion = null;
                return null;
            }

            // Try to remove the already existing reference
            foreach (ITcPlcLibRef item in references)
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

        protected ITcSysManager SystemManager(string projectName = null)
        {
            if (_synchronizationContext != SynchronizationContext.Current)
                throw new Exception("Invalid synchronization context!");

            var ready = false;
            while (!ready)
            {
                try
                {
                    if(!_visualStudio.Dte.Solution.IsOpen)
                        throw new InvalidOperationException("Solution is not open!");


                    if (_visualStudio.Dte.Solution.Projects.Count == 0)
                        throw new InvalidOperationException("There are no projects in this solution!");

                    ready = true;
                    foreach (EnvDTE.Project project in _visualStudio.Dte.Solution.Projects)
                    {
                        if (project == null)
                            ready = false;
                            
                        else if ((projectName == null || project?.Name == projectName) && project.Object as ITcSysManager != null)
                            return project.Object as ITcSysManager;

                    }
                }
                catch(Exception ex)
                {
                    _logger.Trace(ex);
                }

                if (System.Diagnostics.Process.GetProcessesByName("TcXaeShell").Count() == 0)
                    throw new AutomationInterfaceUnresponsiveException(projectName, "TcXaeShell is no longer available - process crashed!", restartHint: true);

                if (!ready)
                    System.Threading.Thread.Sleep(1000);
            }
            
            throw new AutomationInterfaceUnresponsiveException(projectName, "No system manager detected!", restartHint: true);
        }

        protected ITcPlcLibraryManager LibraryManager(string projectName = null, string plcName = null)
        {
            if (_synchronizationContext != SynchronizationContext.Current)
                throw new Exception("Invalid synchronization context!");

            var key = new Tuple<string?, string?>(projectName, plcName);

            var systemManager = SystemManager(projectName);
            bool ready = false;
            while(!ready)
            {
                try
                {
                    if (plcName == null)
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
                        var projectRoot = systemManager.LookupTreeItem($"TIPC^{plcName}");
                        var plc = (projectRoot as ITcProjectRoot)?.NestedProject;
                        for (var k = 1; k <= (plc?.ChildCount ?? 0); k++)
                        {
                            if (plc.Child[k] as ITcPlcLibraryManager != null)
                            {
                                return plc.Child[k] as ITcPlcLibraryManager;
                            }
                        }
                    }

                    ready = true;
                }
                catch (COMException ex)
                {
                    _logger.Trace(ex);
                }

                if (!ready)
                    System.Threading.Thread.Sleep(1000);
            }

            return null;
        }

        protected string GuessDistributorName(ITcPlcLibraryManager libManager, string libraryName, string version)
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

        public override async Task<bool> IsPackageInstalledAsync(PackageItem package)
        {
            if (_referenceCache.Any(x => x.Name == package.PackageVersion.Title && x.DistributorName == package.PackageVersion.DistributorName && x.Version == package.PackageVersion.Version))
                return true;

            await _synchronizationContext;
            return IsPackageInstalled(package);
        }

        public override bool IsPackageInstalled(PackageItem package)
        {
            if (_synchronizationContext != SynchronizationContext.Current)
                throw new Exception("Invalid synchronization context!");

            if (_referenceCache.Any(x => x.Name == package.PackageVersion.Title && x.DistributorName == package.PackageVersion.DistributorName && x.Version == package.PackageVersion.Version))
                return true;

            var libraryManager = LibraryManager(package.ProjectName, package.PlcName);
            bool referenceFound = false;

            if (libraryManager != null)
            {
                foreach (ITcPlcLibrary r in libraryManager.ScanLibraries())
                {
                    if (string.Equals(r.Name, package.PackageVersion.Title, StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(r.Distributor, package.PackageVersion.DistributorName, StringComparison.InvariantCultureIgnoreCase) &&
                        (r.Version == package.PackageVersion.Version || package.PackageVersion.Version == null))
                    {
                        _referenceCache.Add(new PlcLibrary { Name = package.PackageVersion.Title, DistributorName = package.PackageVersion.DistributorName, Version = package.PackageVersion.Version });
                        referenceFound = true;
                        break;
                    }
                }
            }

            return referenceFound;
        }

        public override async Task CloseAllPackageRelatedWindowsAsync(List<PackageItem> packages)
        {
            await SaveAllAsync();
            await _synchronizationContext;
            // Close all windows that have been opened with a library manager
            foreach (EnvDTE.Window window in _visualStudio.Dte.Windows)
            {
                foreach (var package in packages)
                {
                    try
                    {
                        var obj = (window as dynamic).Object;
                        if (obj == null)
                            continue;

                        if (obj.EditorViewFactoryGuid == _libraryManagerGuid)
                        {
                            _logger.Trace($"Closing {package.Catalog?.Name} library manager window");
                            window.Close(vsSaveChanges.vsSaveChangesNo);
                            continue;
                        }

                        if (!(obj.FileName as string).StartsWith("["))
                            continue;

                        if (Regex.Match(obj.FileName, $@"\[{package.PackageVersion.Name}.*?\({package.PackageVersion.DistributorName}\)\].*", RegexOptions.IgnoreCase).Success)
                        {
                            _logger.Trace($"Closing {obj.FileName} window");
                            window.Close(vsSaveChanges.vsSaveChangesNo);
                            continue;
                        }
                    }
                    catch { }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", 
            Justification = "IsPackageInstalled always has to switch to the main context, so it is better to just call it right away")]
        public override async Task AddPackageAsync(PackageItem package)
        {
            await _synchronizationContext;

            // add actual packages
            var libraryManager = LibraryManager(package.ProjectName, package.PlcName);

            var options = package.Config.Options;
            var libraryName = package.PackageVersion.Title;
            var version = package.PackageVersion.Version ?? "*";
            var distributorName = package.PackageVersion.DistributorName ?? GuessDistributorName(libraryManager, libraryName, version);

            // if we can't find the reference with the distributor name from the package, fallback to looking it up
            if (!IsPackageInstalled(package))
                distributorName = GuessDistributorName(libraryManager, libraryName, version);

            // make sure the package is not present before adding it, we have to
            // force, because the package might not even be installed
            await RemovePackageAsync(package, forceRemoval: true);

            if (options?.LibraryReference == true)
                libraryManager.AddLibrary(libraryName, version, distributorName);
            else
                libraryManager.AddPlaceholder(libraryName, libraryName, version, distributorName);

            if (options != null)
            {
                ITcSmTreeItem referenceItem = null;
                ITcSmTreeItem libraryManagerItem = (libraryManager as ITcSmTreeItem);

                if (options?.LibraryReference == true)
                {
                    for (var i = 1; i < libraryManagerItem.ChildCount; i++)
                    {
                        ITcSmTreeItem child = libraryManagerItem.Child[i];
                        string childName = child.Name;
                        if (childName == libraryName)
                        {
                            referenceItem = libraryManagerItem.Child[i];
                            break;
                        }
                    }
                }
                else
                {
                    referenceItem = (libraryManager as ITcSmTreeItem).LookupChild(libraryName);
                }

                if (referenceItem != null)
                {
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
                else
                {
                    _logger.Warn($"Could not apply options to {package.PackageVersion.Name} {package.PackageVersion.Version}");
                }
            }
        }

        public override async Task RemovePackageAsync(PackageItem package, bool uninstall = false, bool forceRemoval = false)
        {
            await _synchronizationContext;

            var libraryManager = LibraryManager(package.ProjectName, package.PlcName);
            var plcLibrary = ResolvePlaceholder(libraryManager, package.PackageVersion.Title, out _, out _);

            try
            {
                if (plcLibrary != null || forceRemoval)
                    libraryManager.RemoveReference(package.PackageVersion.Title);
            }
            catch
            {
                if (!forceRemoval)
                    throw;
            }

            var key = new Tuple<string, string, string>(package.ProjectName, package.PlcName, package.PackageVersion.Title);
            if (_effectiveVersionCache.ContainsKey(key))
                _effectiveVersionCache.Remove(key);

            if (uninstall)
            {
                await UninstallPackageAsync(package);
            }
        }

        public override async Task RemoveAllPackagesAsync(string projectName, string plcName)
        {
            await _synchronizationContext;

            var libraryManager = LibraryManager(projectName, plcName);
            foreach(ITcPlcLibRef reference in libraryManager.References)
                libraryManager.RemoveReference(reference.Name);

            _effectiveVersionCache.Clear();
        }

        public override async Task InstallPackageAsync(PackageItem package, string cachePath = null)
        {
            await _synchronizationContext;

            var libraryManager = LibraryManager(package.ProjectName, package.PlcName);
            var packageVersion = package.PackageVersion;

            var suffix = package.PackageVersion.Compiled == 1 ? "compiled-library" : "library";
            var path = Path.GetFullPath($@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}\{packageVersion.Name}_{packageVersion.Version}.{suffix}");

            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            libraryManager.InstallLibrary("System", path, bOverwrite: true);
        }

        public override async Task<bool> UninstallPackageAsync(PackageItem package)
        {
            await _synchronizationContext;

            if (IsPackageInstalled(package))
            {
                _referenceCache.RemoveAll(x => x.Name == package.PackageVersion.Title && x.DistributorName == package.PackageVersion.DistributorName
                                          && x.Version == package.PackageVersion.Version);

                var libraryManager = LibraryManager(package.ProjectName, package.PlcName);
                libraryManager.UninstallLibrary("System", package.PackageVersion.Title, package.PackageVersion.Version, package.PackageVersion.DistributorName);
                return true;
            }

            return false;
        }

        public override async Task SetPackageVersionAsync(ConfigPlcProject plc, CancellationToken cancellationToken=default)
        {
            await _synchronizationContext;
            var systemManager = SystemManager(plc.ProjectName);
            var projectRoot = systemManager.LookupTreeItem($"TIPC^{plc.Name}") as ITcProjectRoot;
            var iec = projectRoot.NestedProject;
            var allowedPlcNameRegex = new Regex("^[a-zA-Z]+[a-zA-Z0-9_]+$");
            var allowedCompanyRegex = new Regex("^.*$");
            var titleStr = plc.Title ?? plc.Name;

            StringWriter stringWriter = new StringWriter();
            using (XmlWriter writer = XmlWriter.Create(stringWriter))
            {
                writer.WriteStartElement("TreeItem");
                writer.WriteStartElement("IECProjectDef");
                writer.WriteStartElement("ProjectInfo");

                if (!string.IsNullOrEmpty(plc.Name) && allowedPlcNameRegex.IsMatch(plc.Name))
                {
                    writer.WriteElementString("Name", plc.Name);
                    _logger.Info($"Updated title to '{plc.Name}'");
                }
                else
                {
                    _logger.Warn($"Title '{plc.Name}' contains invalid characters - skipping PLC title update, the package might be broken!");
                }

                if (!string.IsNullOrEmpty(titleStr) && allowedPlcNameRegex.IsMatch(titleStr))
                {
                    writer.WriteElementString("Title", titleStr);
                    _logger.Info($"Updated title to '{titleStr}'");
                }
                else
                {
                    _logger.Warn($"Title '{titleStr}' contains invalid characters - skipping PLC title update, the package might be broken!");
                }

                if (!string.IsNullOrEmpty(plc.Version))
                {
                    writer.WriteElementString("Version", new Version(plc.Version).ToString());
                    _logger.Info($"Updated version to '{plc.Version}'");
                }
                else
                {
                    _logger.Warn($"Version '{plc.Version}' is empty - skipping PLC company update, the package might be broken!");
                }

                if (!string.IsNullOrEmpty(plc.DistributorName) && allowedCompanyRegex.IsMatch(plc.DistributorName))
                {
                    writer.WriteElementString("Company", plc.DistributorName);
                    _logger.Info($"Updated company to '{plc.DistributorName}'");
                }
                else
                {
                    _logger.Warn($"Distributor name '{plc.DistributorName}' contains invalid characters - skipping PLC company update, the package might be broken!");
                }
                writer.WriteEndElement();     // ProjectInfo
                writer.WriteEndElement();     // IECProjectDef
                writer.WriteEndElement();     // TreeItem 
            }

            iec.ConsumeXml(stringWriter.ToString());
        }
    }
}
