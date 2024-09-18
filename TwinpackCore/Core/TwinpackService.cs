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
using System.Windows.Navigation;
using Microsoft.VisualStudio.PlatformUI;
using NuGet.Configuration;

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
            public AddPackageOptions() { }
            public AddPackageOptions(AddPackageOptions rhs)
            {
                SkipDownload = rhs.SkipDownload;
                SkipInstall = rhs.SkipInstall;
                ForceDownload = rhs.ForceDownload;
                UpdatePlc = rhs.UpdatePlc;
                IncludeDependencies = rhs.IncludeDependencies;
                DownloadPath = rhs.DownloadPath;
            }

            public bool SkipDownload = false;
            public bool SkipInstall = false;
            public bool ForceDownload = false;
            public bool UpdatePlc = true;
            public bool IncludeDependencies = true;
            public string? DownloadPath = null;
        }
        public class RestorePackageOptions : AddPackageOptions
        {
            public bool IncludeProvidedPackages = false;
            public List<string> ExcludedPackages;
        }

        public class UpdatePackageFilters
        {
            public string ProjectName;
            public string PlcName;
            public string[] Packages;
            public string[] Frameworks;
            public string[] Versions;
            public string[] Branches;
            public string[] Configurations;
            public string[] Targets;
        }

        public class UpdatePackageOptions : AddPackageOptions
        {
            public bool IncludeProvidedPackages = false;
        }

        public class SetPackageVersionOptions
        {
            public bool PurgePackages = false;
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

        private SemaphoreSlim _availablePackagesMutex = new SemaphoreSlim(1, 1);
        private List<PackageItem> _availablePackagesCache = new List<PackageItem>();
        private IAsyncEnumerator<PackageItem> _availablePackagesIt;
        private string _searchTerm;

        private SemaphoreSlim _usedPackagesMutex = new SemaphoreSlim(1,1);
        private List<PackageItem> _usedPackagesCache = new List<PackageItem>();

        private PackageServerCollection _packageServers;
        private Config _config;
        private string _projectName;
        private string _plcName;
        private IAutomationInterface _automationInterface;

        public event PropertyChangedEventHandler PropertyChanged;

        public TwinpackService(PackageServerCollection packageServers, IAutomationInterface automationInterface=null, Config config=null, string projectName=null, string plcName=null)
        {
            _packageServers = packageServers;
            _automationInterface = automationInterface;
            _config = config;
            _projectName = projectName;
            _plcName = plcName;
        }

        public IEnumerable<IPackageServer> PackageServers { get => _packageServers; }
        public bool IsClientUpdateAvailable { get => _packageServers.Any(x => (x as TwinpackServer)?.IsClientUpdateAvailable == true); }
        public bool HasUnknownPackages { get => UsedPackages.Any(x => x.Catalog?.Name == null) == true; }
        public IEnumerable<PackageItem> UsedPackages { get => _usedPackagesCache; }
        public IEnumerable<PackageItem> AvailablePackages { get => _availablePackagesCache; }
        private void CopyRuntimeLicenseIfNeeded(IEnumerable<PackageItem> packages)
        {
            if (_automationInterface == null)
                return;

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

        public async System.Threading.Tasks.Task SaveAsync(string filePath)
        {
            if(_automationInterface != null)
                await _automationInterface.SaveAllAsync();

            _config.FilePath = filePath;
            ConfigFactory.Save(_config);
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
            _availablePackagesCache.Clear();
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
            try
            {
                await _availablePackagesMutex.WaitAsync();

                if (_availablePackagesIt == null || _searchTerm != searchTerm)
                    _availablePackagesIt = _packageServers.SearchAsync(searchTerm, null, batchSize, token).GetAsyncEnumerator();

                _searchTerm = searchTerm;
                var maxPackages = _availablePackagesCache.Count + maxNewPackages;
                while ((maxNewPackages == null || _availablePackagesCache.Count < maxPackages) && (HasMoreAvailablePackages = await _availablePackagesIt.MoveNextAsync()))
                {
                    PackageItem item = _availablePackagesIt.Current;

                    // only add if we don't have this package cached already
                    if (!_availablePackagesCache.Any(x => item.Catalog?.Name == x.Catalog?.Name))
                        _availablePackagesCache.Add(item);
                }

                return _availablePackagesCache
                        .Where(x =>
                            searchTerm == null ||
                            x.Catalog?.DisplayName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                            x.Catalog?.DistributorName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                            x.Catalog?.Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        ;
            }
            finally
            {
                _availablePackagesMutex.Release();
            }
        }

        public async Task<IEnumerable<PackageItem>> RetrieveUsedPackagesAsync(string searchTerm = null, bool includeMetadata = false, CancellationToken token = default)
        {
            try
            {
                await _usedPackagesMutex.WaitAsync();

                foreach (var project in _config.Projects.Where(x => x.Name == _projectName || _projectName == null))
                {
                    foreach (var plc in project.Plcs.Where(x => x.Name == _plcName || _plcName == null))
                    {
                        foreach (var package in plc.Packages.Where(x => _usedPackagesCache.Any(y => y.ProjectName == project.Name && y.PlcName == plc.Name && y.Catalog?.Name == x.Name) == false))
                        {
                            PackageItem catalogItem = await _packageServers.FetchPackageAsync(project.Name, plc.Name, package, includeMetadata, _automationInterface, token);

                            _usedPackagesCache.RemoveAll(x => x.ProjectName == project.Name && x.PlcName == plc.Name && !string.IsNullOrEmpty(x.Catalog?.Name) && x.Catalog?.Name == catalogItem.Catalog?.Name);
                            _usedPackagesCache.Add(catalogItem);

                            if (catalogItem.PackageServer == null)
                                _logger.Warn($"Package {package.Name} (distributor: {package.DistributorName}) referenced in the configuration can not be found on any package server");
                            else
                                _logger.Debug($"Package {package.Name} (distributor: {package.DistributorName}) located on {catalogItem.PackageServer.UrlBase}");
                        }
                    }
                }

                return _usedPackagesCache
                        .Where(x =>
                            searchTerm == null ||
                            x.Catalog?.DisplayName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                            x.Catalog?.DistributorName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                            x.Catalog?.Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        ;
            }
            finally
            {
                _usedPackagesMutex.Release();
            }
        }

        public async System.Threading.Tasks.Task<bool> UninstallPackagesAsync(List<PackageItem> packages, CancellationToken cancellationToken = default)
        {
            foreach (var package in packages)
            {
                if (package.Config.Name == null)
                    throw new Exception("No packages is selected that could be uninstalled!");
            }

            var affectedPackages = await AffectedPackagesAsync(packages, includeDependencies: false, cancellationToken: cancellationToken);

            await _automationInterface.CloseAllPackageRelatedWindowsAsync(affectedPackages);

            var uninstalled = true;
            foreach (var package in affectedPackages.Where(x => packages.Any(y => x.Catalog?.Name == y.Catalog?.Name)))
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

            var affectedPackages = await AffectedPackagesAsync(packages, includeDependencies: false,  cancellationToken: cancellationToken);

            await _automationInterface.CloseAllPackageRelatedWindowsAsync(affectedPackages);

            foreach (var package in affectedPackages.Where(x => packages.Any(y => x.Catalog?.Name == y.Catalog?.Name)))
            {
                _logger.Info($"Removing {package.PackageVersion.Name}");

                if (uninstall)
                    _logger.Info($"Uninstalling {package.PackageVersion.Name} from system ...");

                await _automationInterface.RemovePackageAsync(package, uninstall: uninstall);

                _usedPackagesCache.RemoveAll(x => string.Equals(x.Catalog?.Name, package.PackageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                // update configuration
                var plcConfig = _config?.Projects.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
                plcConfig?.Packages.RemoveAll(x => x.Name == package.PackageVersion.Name);
            }

            if(_automationInterface != null)
                await _automationInterface.SaveAllAsync();

            ConfigFactory.Save(_config);
        }

        public async System.Threading.Tasks.Task<List<PackageItem>> RestorePackagesAsync(RestorePackageOptions options = default, CancellationToken cancellationToken = default)
        {
            var usedPackages = await RetrieveUsedPackagesAsync(token: cancellationToken);
            var packages = usedPackages.Select(x => new PackageItem(x) { Package = x.Used, PackageVersion = x.Used }).ToList();

            // download and add all packages, which are not self references to the provided packages
            var providedPackageNames = _config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();

            if (options?.ExcludedPackages != null)
                providedPackageNames = providedPackageNames.Concat(options?.ExcludedPackages).ToList();

            var providedPackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.Config.Name) == true).ToList(), new AddPackageOptions(options) { SkipDownload = !options.IncludeProvidedPackages }, cancellationToken: cancellationToken);

            var installablePackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.Config.Name) == false).ToList(), options, cancellationToken: cancellationToken);
            installablePackages = options.IncludeProvidedPackages ? providedPackages.Concat(installablePackages).ToList() : installablePackages;

            return installablePackages.GroupBy(x => new { x.ProjectName, x.PlcName, x.PackageVersion.Name, x.PackageVersion.Version })
                .Select(x => x.FirstOrDefault())
                .Distinct()
                .ToList();
        }

        public async System.Threading.Tasks.Task<List<PackageItem>> UpdatePackagesAsync(UpdatePackageFilters filters = default, UpdatePackageOptions options = default, CancellationToken cancellationToken = default)
        {
            var usedPackages = await RetrieveUsedPackagesAsync();
            List<PackageItem> packages;
            if (filters.Packages != null || filters.Frameworks != null)
            {
                packages = usedPackages.Where(
                x => (filters.ProjectName == null || filters.ProjectName == x.ProjectName) &&
                (filters.ProjectName == null || filters.ProjectName == x.ProjectName) &&
                (filters.Packages == null || filters.Packages.Any(y => y == x.Update?.Name)) &&
                (filters.Frameworks == null || filters.Frameworks.Any(y => y == x.Update?.Framework)))
                    .Select(x => new PackageItem(x) { Package = new Protocol.Api.PackageGetResponse(x.Update), PackageVersion = x.Update }).ToList();

                foreach (var package in packages)
                {
                    var i = filters.Packages != null && package.Package.Name != null ? Array.IndexOf(filters.Packages, package.Package.Name) : -1;
                    if (i >= 0)
                    {
                        package.Config.Version = filters.Versions?.ElementAtOrDefault(i);
                        package.Config.Branch = filters.Branches?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.Config.Version) ? package.Config.Branch : null);
                        package.Config.Configuration = filters.Configurations?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.Config.Version) ? package.Config.Configuration : null);
                        package.Config.Target = filters.Targets?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.Config.Version) ? package.Config.Target : null);
                    }

                    i = filters.Frameworks != null && package.PackageVersion.Framework != null ? Array.IndexOf(filters.Frameworks, package.PackageVersion.Framework) : -1;
                    if (i >= 0)
                    {
                        package.Config.Version = filters.Versions?.ElementAtOrDefault(i);
                        package.Config.Branch = filters.Branches?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.Config.Version) ? package.Config.Branch : null);
                        package.Config.Configuration = filters.Configurations?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.Config.Version) ? package.Config.Configuration : null);
                        package.Config.Target = filters.Targets?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.Config.Version) ? package.Config.Target : null);
                    }
                }

                // force new retrievable of metadata
                foreach (var package in packages)
                {
                    package.Package = null;
                    package.PackageVersion = null;
                }
            }
            else
            {
                packages = usedPackages.Select(x => new PackageItem(x) { Package = new Protocol.Api.PackageGetResponse(x.Update), PackageVersion = x.Update }).ToList();
            }

            return await UpdatePackagesAsync(packages, options, cancellationToken);
        }

        protected async System.Threading.Tasks.Task<List<PackageItem>> UpdatePackagesAsync(List<PackageItem> packages, UpdatePackageOptions options = default, CancellationToken cancellationToken = default)
        {
            var usedPackages = await RetrieveUsedPackagesAsync(token: cancellationToken);
            if (packages.Any(x => !usedPackages.Any(y => x.Config.Name == x.Config.Name)))
                throw new ArgumentException("Package to be updated is not used in any project!");

            // download and add all packages, which are not self references to the provided packages
            var providedPackageNames = _config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();
            var providedPackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.Config.Name) == true).ToList(), new AddPackageOptions(options) { SkipDownload = !options.IncludeProvidedPackages }, cancellationToken: cancellationToken);

            var installablePackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.Config.Name) == false).ToList(), options, cancellationToken: cancellationToken);
            installablePackages = options.IncludeProvidedPackages ? providedPackages.Concat(installablePackages).ToList() : installablePackages;

            return installablePackages.GroupBy(x => new { x.ProjectName, x.PlcName, x.PackageVersion.Name, x.PackageVersion.Version })
                .Select(x => x.FirstOrDefault())
                .Distinct()
                .ToList();
        }

        public async System.Threading.Tasks.Task<List<PackageItem>> AddPackageAsync(PackageItem package, AddPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            return await AddPackagesAsync(new List<PackageItem> { package }, options, cancellationToken);
        }

        public async System.Threading.Tasks.Task<List<PackageItem>> AddPackagesAsync(List<PackageItem> packages, AddPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            var addedPackages = new List<PackageItem>();
            if (packages.Any(x => x.Config.Name == null) == true)
                throw new Exception("Invalid package(s) should be added or updated!");

            var affectedPackages = await AffectedPackagesAsync(packages, includeDependencies: true, cancellationToken);
            var downloadPath = options?.DownloadPath ?? $@"{_automationInterface?.SolutionPath ?? "."}\.Zeugwerk\libraries";

            // copy runtime licenses
            CopyRuntimeLicenseIfNeeded(affectedPackages);

            var closeAllPackageRelatedWindowsTask = _automationInterface?.CloseAllPackageRelatedWindowsAsync(affectedPackages) ?? System.Threading.Tasks.Task.Delay(new TimeSpan(0));
            var downloadPackagesTask = options?.SkipDownload == true
                ? System.Threading.Tasks.Task.Run(() => new List<PackageItem>())
                : DownloadPackagesAsync(affectedPackages, 
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
            if(_automationInterface != null && (options?.SkipInstall == null || options?.SkipInstall == false))
            {
                foreach (var package in downloadedPackageVersions)
                {
                    _logger.Info($"Installing {package.PackageVersion.Name} {package.PackageVersion.Version}");
                    await _automationInterface.InstallPackageAsync(package, cachePath: downloadPath); 
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // add affected packages as references
            foreach (var package in (options?.IncludeDependencies == true ? affectedPackages : packages).Where(x => x.PackageVersion?.Name != null))
            {
                _logger.Info($"Adding {package.PackageVersion.Name} {package.PackageVersion.Version} (distributor: {package.PackageVersion.DistributorName})");

                if (_automationInterface != null && (options?.UpdatePlc == null || options?.UpdatePlc == true))
                {
                    await _automationInterface.AddPackageAsync(package);
                }

                var parameters = package.Config.Parameters;

                // delete from package cache so the pac
                _usedPackagesCache.RemoveAll(x => string.Equals(x.Catalog?.Name, package.PackageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                // update configuration
                var plcConfig = _config?.Projects.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
                var packageIndex = plcConfig?.Packages.FindIndex(x => x.Name == package.PackageVersion.Name);
                var newPackageConfig = new ConfigPlcPackage(package.PackageVersion) { Options = package.Config.Options };

                package.Config = newPackageConfig;
                addedPackages.Add(package);

                if (packageIndex.HasValue && packageIndex.Value >= 0)
                    plcConfig.Packages[packageIndex.Value] = newPackageConfig;
                else
                    plcConfig.Packages.Add(newPackageConfig);

                cancellationToken.ThrowIfCancellationRequested();
            }

            if(_automationInterface != null)
                await _automationInterface.SaveAllAsync();

            ConfigFactory.Save(_config);

            return addedPackages;
        }

        public HashSet<string> KnownRuntimeLicenseIds()
        {
            var result = new HashSet<string>();

            if (_automationInterface == null)
                return result;

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
            var resolvedPackage = await _packageServers.FetchPackageAsync(packageItem.ProjectName, packageItem.PlcName, packageItem.Config ?? new ConfigPlcPackage(packageItem), includeMetadata: true, automationInterface: _automationInterface, cancellationToken: cancellationToken);
            packageItem.Config ??= resolvedPackage.Config;
            packageItem.Package = resolvedPackage.Package;
            packageItem.PackageVersion = resolvedPackage.PackageVersion;
            packageItem.PackageServer = resolvedPackage.PackageServer;
        }

        public async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, bool includeDependencies = true, CancellationToken cancellationToken = default)
        {
            return await AffectedPackagesAsync(packages, new List<PackageItem>(), includeDependencies, cancellationToken);
        }

        private async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, List<PackageItem> cache, bool includeDependencies = true, CancellationToken cancellationToken = default)
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
                {
                    _logger.Warn($"Package {package.Config.Name} {package.Config.Version} could not be found!");
                    continue;
                }

                if (cache.Any(x => x.ProjectName == package.ProjectName && x.PlcName == package.PlcName && x.PackageVersion.Name == package.PackageVersion.Name) == false)
                    cache.Add(package);

                if(includeDependencies)
                {
                    var dependencies = package.PackageVersion.Dependencies ?? new List<PackageVersionGetResponse>();
                    await AffectedPackagesAsync(
                        dependencies.Select(x =>
                                    new PackageItem()
                                    {
                                        ProjectName = package.ProjectName,
                                        PlcName = package.PlcName,
                                        Catalog = new CatalogItemGetResponse { Name = x.Name },
                                        Package = x,
                                        PackageVersion = x,
                                        Config = new ConfigPlcPackage(x) { Options = package.Config.Options?.CopyForDependency() }
                                    }).ToList(),
                                    cache,
                                    includeDependencies: true,
                                    cancellationToken: cancellationToken);
                }
            }

            return cache;
        }

        public async Task<List<PackageItem>> DownloadPackagesAsync(List<PackageItem> packages, DownloadPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            List<PackageItem> downloadedPackages = new List<PackageItem> { };
            List<PackageItem> affectedPackages = packages.ToList();

            if (options.IncludeDependencies)
                affectedPackages = await AffectedPackagesAsync(affectedPackages, includeDependencies: true, cancellationToken);

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
            if (options?.PurgePackages == true && options?.SyncFrameworkPackages == false)
                throw new InvalidOperationException("Purging is only viable when framework packages are synchronized!");

            if (options?.PurgePackages == true && _automationInterface != null)
            {
                _logger.Info("Purging packages");
                foreach (var project in _config.Projects)
                {
                    foreach (var plc in project.Plcs)
                    {
                        await _automationInterface.RemoveAllPackagesAsync(project.Name, plc.Name);
                    }
                }
            }

            // set the version of all plcs in the project(s)
            var plcs = _config.Projects
                .Where(x => options?.ProjectName == null || x.Name == options?.ProjectName)
                .SelectMany(x => x.Plcs)
                .Where(x => options?.PlcName == null || x.Name == options?.PlcName);

            foreach(var plc in plcs)
            {
                _logger.Info(new string('-', 3) + $" set-version:{plc.Name}");

                plc.Version = AutomationInterface.NormalizedVersion(version);

                if(_automationInterface != null)
                    await _automationInterface.SetPackageVersionAsync(plc, cancellationToken);
            }

            // also include all framework packages if required
            if (options?.SyncFrameworkPackages == true)
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
                        ProjectName = x.ProjectName,
                        PlcName = x.Name,
                        Config = new ConfigPlcPackage { Name = y.Name }
                    })))
                .GroupBy(x => x.Config.Name)
                .Select(x => x.FirstOrDefault())
                .ToList();

                // resolve plcs packages to get dependencies and the framework they are part of
                affectedPackages = await AffectedPackagesAsync(affectedPackages, includeDependencies: false, cancellationToken);

                var frameworks = affectedPackages
                    .Where(x => x.PackageVersion?.Framework != null && plcs.Any(y => y.Name == x.PackageVersion?.Name))
                    .Select(x => x.PackageVersion?.Framework).Distinct().ToList();

                var frameworkPackages = affectedPackages.Where(x => frameworks.Contains(x.PackageVersion?.Framework)).ToList();

                if (frameworkPackages.Any())
                    _logger.Info("Synchronizing framework packages");

                var nonFrameworkPackages = affectedPackages.Where(x => !frameworks.Contains(x.PackageVersion?.Framework)).ToList();
                var configuredPackages = options?.PurgePackages == true
                    ? plcs.SelectMany(
                        x => x.Packages.Select(y => new PackageItem
                        {
                            ProjectName = x.ProjectName,
                            PlcName = x.Name,
                            Config = y
                        }))
                    .Where(x => frameworkPackages.Any(y => x.Config.Name == y.Config.Name) == false)
                    .Select(
                        x => new PackageItem(x)
                        {
                            Package = new PackageGetResponse(nonFrameworkPackages.FirstOrDefault(y => y.ProjectName == x.ProjectName && y.PlcName == x.PlcName && x.Config.Name == y.Config.Name)?.Package),
                            PackageVersion = new PackageVersionGetResponse(nonFrameworkPackages.FirstOrDefault(y => y.ProjectName == x.ProjectName && y.PlcName == x.PlcName && x.Config.Name == y.Config.Name)?.PackageVersion)
                            { 
                                Title = x.PackageVersion?.Title ?? x.Config.Name,
                                Name = x.Config.Name,
                                DistributorName = x.Config.DistributorName,
                                Version = x.Config.Version,
                                Branch = x.Config.Branch,
                                Target = x.Config.Target,
                                Configuration = x.Config.Configuration,
                            }
                        }
                    )
                    .ToList()
                  : new List<PackageItem>();

                var frameworkPackagesToAdd = new List<PackageItem>();
                foreach (var project in _config.Projects)
                {
                    foreach(var plc in project.Plcs)
                    {
                        var plcPackages = plc.Packages.Where(x => frameworkPackages.Any(y => y.PackageVersion.Name == x.Name)).ToList();

                        foreach (var plcPackage in plcPackages)
                        {
                            var affectedPackage = frameworkPackages.First(y => y.PackageVersion.Name == plcPackage.Name);

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
                                plcPackage.Version = version;
                                plcPackage.Branch = requestedPackage?.Branch;
                                plcPackage.Target = requestedPackage?.Target;
                                plcPackage.Configuration = requestedPackage?.Configuration;

                                // since the package actually exists, we can add it to the plcproj file
                                configuredPackages.Add(new PackageItem(affectedPackage) { ProjectName = project.Name, PlcName = plc.Name, Package = null, PackageVersion = null, Config = plcPackage });
                            }
                            else
                            {
                                plcPackage.Version = version;
                                plcPackage.Branch = options?.PreferredFrameworkBranch;
                                plcPackage.Target = options?.PreferredFrameworkTarget;
                                plcPackage.Configuration = options?.PreferredFrameworkConfiguration;

                                // only a headless interface allows to add not existing packages
                                if (_automationInterface is AutomationInterfaceHeadless)
                                {
                                    frameworkPackagesToAdd.Add(new PackageItem(affectedPackage)
                                    {
                                        ProjectName = project.Name,
                                        PlcName = plc.Name,
                                        PackageVersion = new PackageVersionGetResponse(affectedPackage.PackageVersion)
                                        {
                                            Version = version,
                                            Branch = plcPackage.Branch,
                                            Configuration = plcPackage.Configuration,
                                            Target = plcPackage.Target
                                        },
                                        Config = plcPackage
                                    });
                                }
                            }
                        }
                    }
                }

                // add the packages accordingly to the configuration, which might not exist on the serer
                await AddPackagesAsync(configuredPackages);
                await AddPackagesAsync(frameworkPackagesToAdd, new AddPackageOptions { SkipDownload = true, IncludeDependencies = false });
            }

            if(_automationInterface != null)
                await _automationInterface.SaveAllAsync();

            ConfigFactory.Save(_config);
        }
    }
}
