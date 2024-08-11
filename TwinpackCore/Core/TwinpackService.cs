using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using NLog;
using NLog.Filters;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Caching;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Exceptions;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using Twinpack.Protocol;
using static System.Net.Mime.MediaTypeNames;
using Twinpack.Configuration;

namespace Twinpack.Core
{
    public class TwinpackService : INotifyPropertyChanged
    {
        public class DownloadPackageOptions
        { 
            public bool IncludeProvidedPackages = false;
            public bool IncludeDependencies = true;
            public bool ForceDownload = false;
            public string? DownloadPath = null;
        }

        public class AddPackageOptions
        {
            public bool ForceDownload = false;
            public bool IncludeDependencies = true;
            public string? DownloadPath = null;
        }
        public class RestorePackageOptions : AddPackageOptions
        {
            public bool IncludeProvidedPackages = false;
        }

        public class UpdatePackageOptions : AddPackageOptions
        {
            public bool IncludeProvidedPackages = false;
        }

        public class SetPackageVersionOptions
        {
            public string? ProjectName;
            public string? PlcName;
            public bool SyncFrameworkPackages;
            public string? PreferredFrameworkBranch;
            public string? PreferredFrameworkTarget;
            public string? PreferredFrameworkConfiguration;
        }

        public class ResolvePackageOptions
        {
            public string? PreferredVersion;
            public string? PreferredBranch;
            public string? PreferredTarget;
            public string? PreferredConfiguration;
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private List<PackageItem> _availablePackageCache = new List<PackageItem>();
        private List<PackageItem> _usedPackagesCache = new List<PackageItem>();

        private PackageServerCollection _packageServers;
        private Config _config;
        private IAsyncEnumerator<PackageItem> _availablePackagesIt;
        private string _searchTerm;
        private IAutomationInterface _automationInterface;

        public event PropertyChangedEventHandler PropertyChanged;

        public TwinpackService(PackageServerCollection packageServers, IAutomationInterface automationInterface=null, Config config=null)
        {
            _packageServers = packageServers;
            _automationInterface = automationInterface;
            _config = config;
        }

        public IEnumerable<IPackageServer> PackageServers { get => _packageServers; }
        public bool IsClientUpdateAvailable { get => _packageServers.Any(x => (x as TwinpackServer)?.IsClientUpdateAvailable == true); }
        public bool HasUnknownPackages { get => UsedPackages.Any(x => x.Name == null) == true; }
        public IEnumerable<PackageItem> UsedPackages { get => _usedPackagesCache; }

        private void CopyRuntimeLicenseIfNeeded(IEnumerable<PackageItem> packages)
        {
            var knownLicenseIds = KnownRuntimeLicenseIds();

            foreach (var package in packages)
            {
                if (package.PackageVersion.HasLicenseTmcBinary)
                {
                    _logger.Trace($"Copying license description file to TwinCAT for {package.PackageVersion.Name} ...");
                    try
                    {
                        var licenseId = ParseRuntimeLicenseIdFromTmc(package.PackageVersion.LicenseTmcText);
                        if (licenseId == null)
                            throw new InvalidDataException("The tmc file is not a valid license file!");

                        if (knownLicenseIds.Contains(licenseId))
                        {
                            _logger.Trace($"LicenseId={licenseId} already known");
                        }
                        else
                        {
                            _logger.Info($"Copying license tmc with licenseId={licenseId} to {_automationInterface.LicensesPath}");

                            using (var md5 = MD5.Create())
                            {
                                if (!Directory.Exists(_automationInterface.LicensesPath))
                                    Directory.CreateDirectory(_automationInterface.LicensesPath);

                                File.WriteAllText(Path.Combine(_automationInterface.LicensesPath, BitConverter.ToString(
                                    md5.ComputeHash(Encoding.ASCII.GetBytes($"{package.PackageVersion.DistributorName}{package.PackageVersion.Name}"))).Replace("-", "") + ".tmc"),
                                                  package.PackageVersion.LicenseTmcText);

                            }

                            knownLicenseIds.Add(licenseId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message);
                        _logger.Trace(ex);
                    }
                }
            }
        }

        public static string ParseRuntimeLicenseIdFromTmc(string content)
        {
            try
            {
                var xdoc = XDocument.Parse(content);
                return xdoc.Elements("TcModuleClass")?.Elements("Licenses")?.Elements("License")?.Elements("LicenseId")?.FirstOrDefault()?.Value;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
            }

            return null;
        }

        public void InvalidateCache()
        {
            _availablePackageCache.Clear();
            _usedPackagesCache.Clear();
            _availablePackagesIt = null;
            _packageServers.InvalidateCache();
        }

        public async System.Threading.Tasks.Task LoginAsync(string username=null, string password=null)
        {
            await _packageServers.LoginAsync(username, password);
        }

        bool _hasMoreAvailablePackages;
        public bool HasMoreAvailablePackages
        {
            get => _hasMoreAvailablePackages;
            private set
            {
                if(value != _hasMoreAvailablePackages)
                {
                    _hasMoreAvailablePackages = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMoreAvailablePackages)));
                }
            }
        }

        public async Task<IEnumerable<PackageItem>> RetrieveAvailablePackagesAsync(string searchTerm = null, int? maxNewPackages = null, int batchSize = 5, CancellationToken token = default)
        {
            if(_availablePackagesIt == null || _searchTerm != searchTerm)
                _availablePackagesIt = _packageServers.SearchAsync(searchTerm, null, batchSize, token).GetAsyncEnumerator();

            _searchTerm = searchTerm;
            var maxPackages = _availablePackageCache.Count + maxNewPackages;
            while ((maxNewPackages == null || _availablePackageCache.Count < maxPackages) && (HasMoreAvailablePackages = await _availablePackagesIt.MoveNextAsync()))
            {
                PackageItem item = _availablePackagesIt.Current;

                // only add if we don't have this package cached already
                if(!_availablePackageCache.Any(x => item.Name == x.Name))
                    _availablePackageCache.Add(item);
            }

            return _availablePackageCache
                    .Where(x =>
                        searchTerm == null ||
                        x.DisplayName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.DistributorName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    ;
        }

        public async Task<IEnumerable<PackageItem>> RetrieveUsedPackagesAsync(string searchTerm = null, bool includeMetadata = false, CancellationToken token = default)
        {
            foreach (var project in _config.Projects)
            {
                foreach (var plc in project.Plcs)
                {
                    foreach (var package in plc.Packages.Where(x => _usedPackagesCache.Any(y => y.ProjectName == project.Name && y.PlcName == plc.Name && y.Name == x.Name) == false))
                    {
                        PackageItem catalogItem = await _packageServers.FetchPackageAsync(project.Name, plc.Name, package, includeMetadata, _automationInterface, token);

                        _usedPackagesCache.RemoveAll(x => x.ProjectName == project.Name && x.PlcName == plc.Name && !string.IsNullOrEmpty(x.Name) && x.Name == catalogItem.Name);
                        _usedPackagesCache.Add(catalogItem);

                        if (catalogItem.PackageServer == null)
                            _logger.Warn($"Package {package.Name} {package.Version} (distributor: {package.DistributorName}) referenced in the configuration can not be found on any package server");
                        else
                            _logger.Info($"Package {package.Name} {package.Version} (distributor: {package.DistributorName}) located on {catalogItem.PackageServer.UrlBase}");
                    }
                }
            }

            return _usedPackagesCache
                    .Where(x =>
                        searchTerm == null ||
                        x.DisplayName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.DistributorName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    ;
        }

        public async System.Threading.Tasks.Task<bool> UninstallPackagesAsync(List<PackageItem> packages, CancellationToken cancellationToken = default)
        {
            foreach (var package in packages)
            {
                if (package.Config.Name == null)
                    throw new Exception("No packages is selected that could be uninstalled!");
            }

            var affectedPackages = await AffectedPackagesAsync(packages, cancellationToken);

            await _automationInterface.CloseAllPackageRelatedWindowsAsync(affectedPackages);

            var uninstalled = true;
            foreach (var package in affectedPackages.Where(x => packages.Any(y => x.Name == y.Name)))
                uninstalled &= await _automationInterface.UninstallPackageAsync(package);

            return uninstalled;
        }

        public async System.Threading.Tasks.Task RemovePackagesAsync(List<PackageItem> packages, bool uninstall=false, CancellationToken cancellationToken = default)
        {
            foreach (var package in packages)
            {
                if (package.Config.Name == null)
                    throw new Exception("No packages is selected that could be uninstalled!");
            }

            var affectedPackages = await AffectedPackagesAsync(packages, cancellationToken);

            await _automationInterface.CloseAllPackageRelatedWindowsAsync(affectedPackages);

            foreach (var package in affectedPackages.Where(x => packages.Any(y => x.Name == y.Name)))
            {
                _logger.Info($"Removing {package.PackageVersion.Name}");

                if (uninstall)
                    _logger.Info($"Uninstalling {package.PackageVersion.Name} from system ...");

                await _automationInterface.RemovePackageAsync(package, uninstall: uninstall);

                _usedPackagesCache.RemoveAll(x => string.Equals(x.Name, package.PackageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                // update configuration
                var plcConfig = _config?.Projects.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
                plcConfig?.Packages.RemoveAll(x => x.Name == package.PackageVersion.Name);
            }

            _automationInterface.SaveAll();
            ConfigFactory.Save(_config);
        }

        public async System.Threading.Tasks.Task RestorePackagesAsync(RestorePackageOptions options = default, CancellationToken cancellationToken = default)
        {
            var usedPackages = await RetrieveUsedPackagesAsync(token: cancellationToken);

            var packages = usedPackages.Select(x => new PackageItem(x) { Package = x.Used, PackageVersion = x.Used }).ToList();

            // ignore packages, which are provided by the loaded configuration
            if (!options.IncludeProvidedPackages)
            {
                var providedPackageNames = _config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();
                packages = packages.Where(x => providedPackageNames.Any(y => y == x.Config.Name) == false).ToList();
            }

            await AddPackagesAsync(packages, options, cancellationToken: cancellationToken);
        }

        public async System.Threading.Tasks.Task UpdatePackagesAsync(UpdatePackageOptions options = default, CancellationToken cancellationToken = default)
        {
            var usedPackages = await RetrieveUsedPackagesAsync(token: cancellationToken);

            var packages = usedPackages.Select(x => new PackageItem(x) { Package = x.Update, PackageVersion = x.Update }).ToList();

            // ignore packages, which are provided by the loaded configuration
            if (!options.IncludeProvidedPackages)
            {
                var providedPackageNames = _config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();
                packages = packages.Where(x => providedPackageNames.Any(y => y == x.Config.Name) == false).ToList();
            }

            await AddPackagesAsync(packages, options, cancellationToken: cancellationToken);
        }

        public async System.Threading.Tasks.Task AddPackageAsync(PackageItem package, AddPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            await AddPackagesAsync(new List<PackageItem> { package }, options, cancellationToken);
        }

        public async System.Threading.Tasks.Task AddPackagesAsync(List<PackageItem> packages, AddPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            if (packages.Any(x => x.Config.Name == null) == true)
                throw new Exception("Invalid package(s) should be added or updated!");

            var affectedPackages = await AffectedPackagesAsync(packages, cancellationToken);
            var downloadPath = options?.DownloadPath ?? $@"{_automationInterface.SolutionPath}\.Zeugwerk\libraries";

            // copy runtime licenses
            CopyRuntimeLicenseIfNeeded(affectedPackages);

            var closeAllPackageRelatedWindowsTask = _automationInterface.CloseAllPackageRelatedWindowsAsync(affectedPackages);
            var downloadPackagesTask = DownloadPackagesAsync(affectedPackages, 
                new DownloadPackageOptions
                {
                    IncludeProvidedPackages = true, 
                    IncludeDependencies = options?.IncludeDependencies == true, 
                    ForceDownload = options?.ForceDownload == true, 
                    DownloadPath = downloadPath
                }, cancellationToken: cancellationToken);
            await System.Threading.Tasks.Task.WhenAll(closeAllPackageRelatedWindowsTask, downloadPackagesTask);

            var downloadedPackageVersions = await downloadPackagesTask;

            // install downloaded packages
            foreach (var package in downloadedPackageVersions)
            {
                _logger.Info($"Installing {package.PackageVersion.Name} {package.PackageVersion.Version}");
                await _automationInterface.InstallPackageAsync(package, cachePath: downloadPath);
                cancellationToken.ThrowIfCancellationRequested();
            }


            // add affected packages as references
            foreach (var package in options?.IncludeDependencies == true ? affectedPackages : packages)
            {
                _logger.Info($"Adding {package.PackageVersion.Name} {package.PackageVersion.Version} (distributor: {package.PackageVersion.DistributorName})");
                await _automationInterface.RemovePackageAsync(package);
                await _automationInterface.AddPackageAsync(package);
                cancellationToken.ThrowIfCancellationRequested();

                var parameters = package.Config.Parameters;

                // delete from package cache so the pac
                _usedPackagesCache.RemoveAll(x => string.Equals(x.Name, package.PackageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                // update configuration
                var plcConfig = _config?.Projects.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
                var packageIndex = plcConfig?.Packages.FindIndex(x => x.Name == package.PackageVersion.Name);
                var newPackageConfig = new ConfigPlcPackage(package.PackageVersion) { Options = package.Config.Options };

                if (packageIndex.HasValue && packageIndex.Value >= 0)
                    plcConfig.Packages[packageIndex.Value] = newPackageConfig;
                else
                    plcConfig.Packages.Add(newPackageConfig);

                cancellationToken.ThrowIfCancellationRequested();
            }

            _automationInterface.SaveAll();
            ConfigFactory.Save(_config);
        }

        public HashSet<string> KnownRuntimeLicenseIds()
        {
            var result = new HashSet<string>();
            if (!Directory.Exists(_automationInterface.LicensesPath))
                return result;

            foreach (var fileName in Directory.GetFiles(_automationInterface.LicensesPath, "*.tmc", SearchOption.AllDirectories))
            {
                try
                {
                    var licenseId = ParseRuntimeLicenseIdFromTmc(File.ReadAllText(fileName));

                    if (licenseId == null)
                        throw new InvalidDataException($"The file {fileName} is not a valid license file!");

                    result.Add(licenseId);
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex);
                }
            }

            return result;
        }

        public async System.Threading.Tasks.Task FetchPackageAsync(PackageItem packageItem, CancellationToken cancellationToken = default)
        {
            var resolvedPackage = await _packageServers.FetchPackageAsync(packageItem.ProjectName, packageItem.PlcName, packageItem.Config, includeMetadata: true, automationInterface: _automationInterface, cancellationToken: cancellationToken);
            packageItem.Package ??= resolvedPackage.Package;
            packageItem.PackageVersion ??= resolvedPackage.PackageVersion;
        }

        public async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, CancellationToken cancellationToken = default)
        {
            return await AffectedPackagesAsync(packages, new List<PackageItem>(), cancellationToken);
        }

        private async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, List<PackageItem> cache, CancellationToken cancellationToken = default)
        {
            foreach(var package in packages)
            {
                if (package.Package == null || package.PackageVersion == null)
                {
                    var resolvedPackage = await _packageServers.FetchPackageAsync(package.ProjectName, package.PlcName, package.Config, includeMetadata: true, _automationInterface, cancellationToken);
                    package.Package ??= resolvedPackage.Package;
                    package.PackageVersion ??= resolvedPackage.PackageVersion;
                }

                if (package.PackageVersion?.Name == null)
                    break;

                if (cache.Any(x => x.ProjectName == package.ProjectName && x.PlcName == package.PlcName && x.PackageVersion.Name == package.PackageVersion.Name) == false)
                    cache.Add(package);

                var dependencies = package.PackageVersion.Dependencies ?? new List<PackageVersionGetResponse>();
                await AffectedPackagesAsync(
                    dependencies.Select(x =>
                                new PackageItem()
                                {
                                    Name = x.Name,
                                    ProjectName = package.ProjectName,
                                    PlcName = package.PlcName,
                                    Package = x,
                                    PackageVersion = x,
                                    Config = new ConfigPlcPackage(x) { Options = package.Config.Options?.CopyForDependency() }
                                }).ToList(),
                                cache,
                                cancellationToken: cancellationToken);
            }

            return cache;
        }

        public async Task<List<PackageItem>> DownloadPackagesAsync(List<PackageItem> packages, DownloadPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            List<PackageItem> downloadedPackages = new List<PackageItem> { };
            List<PackageItem> affectedPackages = packages.ToList();

            if (options.IncludeDependencies)
                affectedPackages = await AffectedPackagesAsync(affectedPackages, cancellationToken);

            // avoid downloading duplicates
            affectedPackages = affectedPackages.GroupBy(x => new
            {
                x.PackageVersion.Name,
                x.PackageVersion.Version,
                x.PackageVersion.Branch,
                x.PackageVersion.Target,
                x.PackageVersion.Configuration
            }).Select(x => x.First()).ToList();

            if (!options.ForceDownload && _automationInterface == null)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            // ignore packages, which are provided by the loaded configuration
            if(!options.IncludeProvidedPackages)
            {
                var providedPackageNames = _config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();
                affectedPackages = affectedPackages.Where(x => providedPackageNames.Any(y => y == x.PackageVersion.Name) == false).ToList();
            }

            foreach(var affectedPackage in affectedPackages)
            {
                // check if we find the package on the system
                bool referenceFound = !options.ForceDownload && _automationInterface != null && await _automationInterface.IsPackageInstalledAsync(affectedPackage);

                if (!referenceFound || options.ForceDownload)
                {
                    if (await _packageServers.DownloadPackageVersionAsync(affectedPackage, options.DownloadPath, cancellationToken))
                        downloadedPackages.Add(affectedPackage);
                }
            }

            return downloadedPackages;
        }

        public bool IsPackageInstalled(PackageItem package)
        {
            return _automationInterface.IsPackageInstalled(package);
        }

        public async System.Threading.Tasks.Task SetPackageVersionAsync(string version, SetPackageVersionOptions options = default, CancellationToken cancellationToken = default)
        {
            // set the version of all plcs in the project(s)
            var plcs = _config.Projects
                .Where(x => options.ProjectName == null || x.Name == options.ProjectName)
                .SelectMany(x => x.Plcs)
                .Where(x => options.PlcName == null || x.Name == options.PlcName);

            foreach(var plc in plcs)
            {
                plc.Version = version;
                await _automationInterface?.SetPackageVersionAsync(plc, cancellationToken);
            }

            // also include all framework packages if required
            if (options.SyncFrameworkPackages)
            {
                // resolve the plcs as packages
                var affectedPackages = plcs.Select(x => new PackageItem
                {
                    ProjectName = x.ProjectName,
                    PlcName = x.Name,
                    Config = new ConfigPlcPackage { Name = x.Name }
                }).ToList();

                affectedPackages = affectedPackages.Concat(plcs.SelectMany(
                    x => x.Packages.Select(y => new PackageItem
                    {
                        Config = new ConfigPlcPackage { Name = y.Name }
                    })))
                .GroupBy(x => x.Config.Name)
                .Select(x => x.FirstOrDefault())
                .ToList();

                // resolve plcs packages to get dependencies and the framework they are part of
                affectedPackages = await AffectedPackagesAsync(affectedPackages, cancellationToken);

                var frameworks = affectedPackages
                    .Where(x => x.PackageVersion.Framework != null && plcs.Any(y => y.Name == x.PackageVersion.Name))
                    .Select(x => x.PackageVersion.Framework).Distinct().ToList();

                affectedPackages = affectedPackages.Where(x => frameworks.Contains(x.PackageVersion.Framework)).ToList();

                foreach(var project in _config.Projects)
                {
                    foreach(var plc in project.Plcs)
                    {
                        var plcPackages = plc.Packages.Where(x => affectedPackages.Any(y => y.PackageVersion.Name == x.Name)).ToList();
                        var packageToOverwrite = new List<PackageItem>();
                        foreach (var plcPackage in plcPackages)
                        {
                            var affectedPackage = affectedPackages.First(y => y.PackageVersion.Name == plcPackage.Name);

                            // check if the requested version is actually on a package server already
                            var requestedPackage = await _packageServers.ResolvePackageAsync(plcPackage.Name,
                                new ResolvePackageOptions
                                {
                                    PreferredVersion = version,
                                    PreferredBranch = options?.PreferredFrameworkBranch,
                                    PreferredTarget = options?.PreferredFrameworkTarget,
                                    PreferredConfiguration = options?.PreferredFrameworkConfiguration
                                });

                            if (requestedPackage?.Version == version)
                            {
                                // since the package actually exists, we can add it to the plcproj file
                                plcPackage.Version = version;
                                plcPackage.Branch = requestedPackage?.Branch;
                                plcPackage.Target = requestedPackage?.Target;
                                plcPackage.Configuration = requestedPackage?.Configuration;

                                packageToOverwrite.Add(new PackageItem(affectedPackage) { ProjectName = project.Name, PlcName = plc.Name, Package = null, PackageVersion = null, Config = plcPackage });
                            }
                            else
                            {
                                plcPackage.Version = version;
                                plcPackage.Branch = options?.PreferredFrameworkBranch;
                                plcPackage.Target = options?.PreferredFrameworkTarget;
                                plcPackage.Configuration = options?.PreferredFrameworkConfiguration;
                            }
                        }

                        foreach (var package in packageToOverwrite)
                            await AddPackageAsync(package);
                    }
                }
            }

            _automationInterface?.SaveAll();
            ConfigFactory.Save(_config);
        }
    }
}