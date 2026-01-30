using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using Twinpack.Protocol;
using Twinpack.Configuration;
using System.Text.RegularExpressions;

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
                SkipDownload = rhs?.SkipDownload ?? false;
                SkipInstall = rhs?.SkipInstall ?? false;
                ForceDownload = rhs?.ForceDownload ??false;
                UpdatePlc = rhs?.UpdatePlc ?? true;
                IncludeDependencies = rhs?.IncludeDependencies ?? true;
                DownloadPath = rhs?.DownloadPath;
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

        public class Artifact
        {
            public Artifact(Config config, ConfigPlcProject plc)
            {
                Config = config;
                Plc = plc;
            }

            public Config Config;
            public ConfigPlcProject Plc;

            public string ArtifactName
            {
                get
                {
                    return Plc.Name;
                }

                set
                {
                    Plc.Name = value;
                }
            }
            public string ArtifactVersion
            {
                get
                {
                    return Plc.Version;
                }

                set
                {
                    Plc.Version = value;
                }
            }

            public ConfigPlcPackage ConfigPlcPackage {
                get => new ConfigPlcPackage
                {
                    Name = ArtifactName
                };
            }

            public PackageItem PackageItem {
                get => new PackageItem
                {
                    ProjectName = Plc.ProjectName,
                    PlcName = Plc.Name,
                    Config = ConfigPlcPackage
                };
            }

            public List<PackageItem> AffectedPackages
            {
                get
                {
                    var affectedPackages = PackageItem;
                    return Plc.Packages.Select(y => new PackageItem
                    {
                        ProjectName = Plc.ProjectName,
                        PlcName = Plc.Name,
                        Config = new ConfigPlcPackage { Name = y.Name }
                    })
                    .Prepend(PackageItem)
                    .ToList();
                }
            }

            public List<PackageItem> Packages
            {
                get
                {
                    var affectedPackages = PackageItem;
                    return Plc.Packages.Select(y => new PackageItem
                    {
                        ProjectName = Plc.ProjectName,
                        PlcName = Plc.Name,
                        Config = y
                    })
                    .ToList();
                }
            }
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
        private static void CopyRuntimeLicenseIfNeeded(IAutomationInterface automationInterface, IEnumerable<PackageItem> packages)
        {
            if (automationInterface == null)
                return;

            var knownLicenseIds = KnownRuntimeLicenseIds(automationInterface);

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
                            _logger.Info($"Copying license tmc with licenseId={licenseId} to {automationInterface.LicensesPath}");

                            using (var md5 = MD5.Create())
                            {
                                if (!Directory.Exists(automationInterface.LicensesPath))
                                    Directory.CreateDirectory(automationInterface.LicensesPath);

                                File.WriteAllText(Path.Combine(automationInterface.LicensesPath, BitConverter.ToString(
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
            if (content == null)
                return null;

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

        public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
        {
            _availablePackagesCache.Clear();
            _usedPackagesCache.Clear();

            if (_availablePackagesIt != null)
                await _availablePackagesIt.DisposeAsync();

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
                await _availablePackagesMutex.WaitAsync(token);

                if (_availablePackagesIt == null || _searchTerm != searchTerm)
                {
                    if (_availablePackagesIt != null)
                        await _availablePackagesIt.DisposeAsync();

                    _availablePackagesIt = _packageServers.SearchAsync(searchTerm, null, batchSize).GetAsyncEnumerator();

                    _searchTerm = searchTerm;
                }

                var maxPackages = _availablePackagesCache.Count + maxNewPackages;
                token.ThrowIfCancellationRequested();
                while ((maxNewPackages == null || _availablePackagesCache.Count < maxPackages) && (HasMoreAvailablePackages = await _availablePackagesIt.MoveNextAsync()))
                {            
                    PackageItem item = _availablePackagesIt.Current;

                    // only add if we don't have this package cached already
                    if (!_availablePackagesCache.Any(x => item.Catalog?.Name == x.Catalog?.Name))
                        _availablePackagesCache.Add(item);

                    token.ThrowIfCancellationRequested();
                }

                Regex rx = searchTerm != null ? new Regex(".*?" + searchTerm.Replace(" ", ".") + ".*?", RegexOptions.Compiled | RegexOptions.IgnoreCase) : null;
                return _availablePackagesCache
                        .Where(x =>
                            rx == null ||
                            rx?.Match(x.Catalog?.Name).Success == true ||
                            rx?.Match(x.Catalog?.DisplayName).Success == true ||
                            rx?.Match(x.Catalog?.DistributorName).Success == true)
                        ;
            }
            catch (OperationCanceledException ex)
            {
                throw;
            }
            finally
            {
                _availablePackagesMutex.Release();
            }
        }

        public async Task<IEnumerable<PackageItem>> RetrieveUsedPackagesAsync(string searchTerm = null, bool includeMetadata = false, List<string> excludedPackages = null, CancellationToken token = default)
        {
            if (_config.Modules?.Any() == true)
                throw new NotSupportedException("Modules are not supported");

            try
            {
                await _usedPackagesMutex.WaitAsync();

                foreach (var project in _config.Projects.Where(x => x.Name == _projectName || _projectName == null))
                {
                    foreach (var plc in project.Plcs.Where(x => x.Name == _plcName || _plcName == null))
                    {
                        foreach (var package in plc.Packages
                            .Where(x => excludedPackages == null || !excludedPackages.Contains(x.Name))
                            .Where(x => _usedPackagesCache.Any(y => y.ProjectName == project.Name && y.PlcName == plc.Name && y.Catalog?.Name == x.Name) == false))
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
            if (_config.Modules?.Any() == true)
                throw new NotSupportedException("Modules are not supported");

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
            if (_config.Modules?.Any() == true)
                throw new NotSupportedException("Modules are not supported");

            var usedPackages = await RetrieveUsedPackagesAsync(excludedPackages: options?.ExcludedPackages, token: cancellationToken);

            // download and add all packages, which are not self references to the provided packages
            var providedPackageNames = _config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();

            if (options?.ExcludedPackages != null)
                providedPackageNames = providedPackageNames.Concat(options?.ExcludedPackages).ToList();

            var packages = usedPackages.Select(x => new PackageItem(x) { Package = x.Used, PackageVersion = x.Used }).ToList();
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
            if (filters?.Packages != null || filters?.Frameworks != null)
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
                        package.Config.Version = string.IsNullOrEmpty(filters.Versions?.ElementAtOrDefault(i)) ? null : filters.Versions?.ElementAtOrDefault(i);
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
            if (_config.Modules?.Any() == true)
                throw new NotSupportedException("Modules are not supported");

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

        public async Task<List<PackageItem>> AddPackageAsync(PackageItem package, AddPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            return await AddPackagesAsync(new List<PackageItem> { package }, options, cancellationToken);
        }


        public async Task<List<PackageItem>> AddPackagesAsync(List<PackageItem> packages, AddPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            return await AddPackagesAsync(_config, _automationInterface, _packageServers, _usedPackagesCache, packages, options, cancellationToken);
        }

        private static async Task<List<PackageItem>> AddPackagesAsync(Config config, IAutomationInterface automationInterface, PackageServerCollection packageServers, List<PackageItem> cache, List<PackageItem> packages, AddPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            if (config.Modules?.Any() == true)
                throw new NotSupportedException("Modules are not supported");

            var addedPackages = new List<PackageItem>();
            if (packages.Any(x => x.Config.Name == null) == true)
                throw new Exception("Invalid package(s) should be added or updated!");

            var affectedPackages = await AffectedPackagesAsync(automationInterface, packageServers, packages, includeDependencies: true, cache: new List<PackageItem>(), cancellationToken: cancellationToken);
            var downloadPath = options?.DownloadPath ?? $@"{automationInterface?.SolutionPath ?? "."}\.Zeugwerk\libraries";

            // copy runtime licenses
            CopyRuntimeLicenseIfNeeded(automationInterface, affectedPackages);

            var closeAllPackageRelatedWindowsTask = automationInterface?.CloseAllPackageRelatedWindowsAsync(affectedPackages) ?? Task.Delay(new TimeSpan(0));
            var downloadPackagesTask = options?.SkipDownload == true
                ? Task.Run(() => new List<PackageItem>())
                : DownloadPackagesAsync(config, automationInterface, packageServers, affectedPackages, 
                new DownloadPackageOptions
                {
                    IncludeProvidedPackages = true, 
                    IncludeDependencies = options?.IncludeDependencies == true, 
                    ForceDownload = options?.ForceDownload == true, 
                    DownloadPath = downloadPath
                }, cancellationToken: cancellationToken);
            await Task.WhenAll(closeAllPackageRelatedWindowsTask, downloadPackagesTask);

            var downloadedPackageVersions = await downloadPackagesTask;

            // install downloaded packages
            if(automationInterface != null && (options?.SkipInstall == null || options?.SkipInstall == false))
            {
                foreach (var package in downloadedPackageVersions)
                {
                    _logger.Info($"Installing {package.PackageVersion.Name} {package.PackageVersion.Version}");
                    await automationInterface.InstallPackageAsync(package, cachePath: downloadPath); 
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            // add affected packages as references
            foreach (var package in (options?.IncludeDependencies == true ? affectedPackages : packages).Where(x => x.PackageVersion?.Name != null))
            {
                _logger.Info($"Adding {package.PackageVersion.Name} {package.PackageVersion.Version} (distributor: {package.PackageVersion.DistributorName}) to {package.ProjectName}/{package.PlcName}");

                if (automationInterface != null && (options?.UpdatePlc == null || options?.UpdatePlc == true))
                {
                    await automationInterface.AddPackageAsync(package);
                }

                var parameters = package.Config.Parameters;

                // delete from package cache so the pac
                cache.RemoveAll(x => string.Equals(x.Catalog?.Name, package.PackageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                // update configuration
                var plcConfig = config?.Projects.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
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

            if(automationInterface != null)
                await automationInterface.SaveAllAsync();

            ConfigFactory.Save(config);

            return addedPackages;
        }

        public HashSet<string> KnownRuntimeLicenseIds()
        {
            return KnownRuntimeLicenseIds(_automationInterface);
        }

        public static HashSet<string> KnownRuntimeLicenseIds(IAutomationInterface automationInterface)
        {
            var result = new HashSet<string>();

            if (automationInterface == null)
                return result;

            if (!Directory.Exists(automationInterface.LicensesPath))
                return result;

            foreach (var fileName in Directory.GetFiles(automationInterface.LicensesPath, "*.tmc", SearchOption.AllDirectories))
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

        public async Task FetchPackageAsync(PackageItem packageItem, CancellationToken cancellationToken = default)
        {
            var resolvedPackage = await _packageServers.FetchPackageAsync(packageItem.PackageServer, packageItem.ProjectName, packageItem.PlcName, packageItem.Config ?? new ConfigPlcPackage(packageItem), includeMetadata: true, automationInterface: _automationInterface, cancellationToken: cancellationToken);
            packageItem.Config ??= resolvedPackage.Config;
            packageItem.Package = resolvedPackage.Package;
            packageItem.PackageVersion = resolvedPackage.PackageVersion;
            packageItem.PackageServer = resolvedPackage.PackageServer;
        }

        public async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, bool includeDependencies = true, CancellationToken cancellationToken = default)
        {
            return await AffectedPackagesAsync(packages, new List<PackageItem>(), includeDependencies, cancellationToken);
        }

        public async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, List<PackageItem> cache, bool includeDependencies = true, CancellationToken cancellationToken = default)
        {
            return await AffectedPackagesAsync(_automationInterface, _packageServers, packages, cache, includeDependencies, cancellationToken);
        }

        private static async Task<List<PackageItem>> AffectedPackagesAsync(IAutomationInterface automationInterface, PackageServerCollection packageServers, List<PackageItem> packages, List<PackageItem> cache, bool includeDependencies = true, CancellationToken cancellationToken = default)
        {
            foreach (var package in packages)
            {
                if (package.Package == null || package.PackageVersion == null || package.PackageServer == null)
                {
                    package.Package = null;
                    package.PackageVersion = null;
                    package.PackageServer = null;
                    var resolvedPackage = await packageServers.FetchPackageAsync(package.PackageServer, package.ProjectName, package.PlcName, package.Config, includeMetadata: true, automationInterface, cancellationToken);
                    package.Package ??= resolvedPackage.Package;
                    package.PackageVersion ??= resolvedPackage.PackageVersion;
                    package.PackageServer ??= resolvedPackage.PackageServer;
                }

                if (package.PackageVersion?.Name == null)
                {
                    _logger.Trace($"Package {package.Config.Name} {package.Config.Version ?? "*"} could not be found!");
                    continue;
                }

                // todo: remove
                if (cache.Any(x => x.ProjectName == package.ProjectName && x.PlcName == package.PlcName && x.PackageVersion.Name == package.PackageVersion.Name) == false)
                    cache.Add(package);
            }

            if (includeDependencies)
            {
                foreach (var package in packages)
                {
                    var dependencies = package.PackageVersion?.Dependencies ?? new List<PackageVersionGetResponse>();
                    await AffectedPackagesAsync(automationInterface, packageServers,
                        dependencies.Select(x =>
                                    new PackageItem()
                                    {
                                        PackageServer = packages.Where(y => (y.Config?.Name ?? y.PackageVersion?.Name) == x.Name).FirstOrDefault()?.PackageServer,
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

            foreach (var package in packages.Where(x => x.PackageVersion?.Name != null))
            {
                if (cache.Any(x => x.ProjectName == package.ProjectName && x.PlcName == package.PlcName && x.PackageVersion.Name == package.PackageVersion.Name) == false)
                {
                    cache.Add(package);
                }
            }

            return cache;
        }

        public async Task<List<PackageItem>> DownloadPackagesAsync(List<PackageItem> packages, DownloadPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            return await DownloadPackagesAsync(_config, _automationInterface, _packageServers, packages, options, cancellationToken);
        }

        private static async Task<List<PackageItem>> DownloadPackagesAsync(Config config, IAutomationInterface automationInterface, PackageServerCollection packageServers, List<PackageItem> packages, DownloadPackageOptions options = default, CancellationToken cancellationToken = default)
        {            
            List<PackageItem> downloadedPackages = new List<PackageItem> { };
            List<PackageItem> affectedPackages = packages.ToList();

            if (options.IncludeDependencies)
                affectedPackages = await AffectedPackagesAsync(automationInterface, packageServers, affectedPackages, cache: new List<PackageItem>(), includeDependencies: true, cancellationToken);

            // avoid downloading duplicates
            affectedPackages = affectedPackages.GroupBy(x => new
            {
                x.PackageVersion.Name,
                x.PackageVersion.Version,
                x.PackageVersion.Branch,
                x.PackageVersion.Target,
                x.PackageVersion.Configuration
            }).Select(x => x.First()).ToList();

            if (!options.ForceDownload && automationInterface == null)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            // ignore packages, which are provided by the loaded configuration
            if(!options.IncludeProvidedPackages)
            {
                var providedPackageNames = config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();
                affectedPackages = affectedPackages.Where(x => providedPackageNames.Any(y => y == x.PackageVersion.Name) == false).ToList();
            }

            foreach(var affectedPackage in affectedPackages)
            {
                // check if we find the package on the system
                bool referenceFound = !options.ForceDownload && automationInterface != null && await automationInterface.IsPackageInstalledAsync(affectedPackage);

                if (!referenceFound || options.ForceDownload)
                {
                    if (await packageServers.DownloadPackageVersionAsync(affectedPackage, options.DownloadPath, cancellationToken))
                        downloadedPackages.Add(affectedPackage);
                }
            }

            return downloadedPackages;
        }

        public bool IsPackageInstalled(PackageItem package)
        {
            return _automationInterface.IsPackageInstalled(package);
        }

        private async Task PurgePackagesAsync(Config config, IAutomationInterface automationInterface)
        {
            foreach (var project in config.Projects)
            {
                foreach (var plc in project.Plcs)
                {
                    await automationInterface.RemoveAllPackagesAsync(project.Name, plc.Name);
                }
            }
        }

        private async Task<List<Artifact>> SetPackageVersionAsync(Config config, IAutomationInterface automationInterface, string version, SetPackageVersionOptions options, CancellationToken cancellationToken)
        {
            // set the version of all plcs in the project(s)
            var plcs = config.Projects
                .Where(x => options?.ProjectName == null || x.Name == options?.ProjectName)
                .SelectMany(x => x.Plcs)
                .Where(x => options?.PlcName == null || x.Name == options?.PlcName)
                ;

            List<Artifact> artifacts = new List<Artifact>();
            foreach (var plc in plcs)
            {
                _logger.Info(new string('-', 3) + $" set-version:{plc.Name}");

                plc.Version = version;

                if (_automationInterface != null)
                    await automationInterface.SetPackageVersionAsync(plc, cancellationToken);

                artifacts.Add(new Artifact(config, plc));
            }

            await automationInterface.SaveAllAsync();
            ConfigFactory.Save(config);

            return artifacts;
        }

        public async Task SetPackageVersionAsync(string version, SetPackageVersionOptions options = default, CancellationToken cancellationToken = default)
        {
            if (options?.PurgePackages == true && options?.SyncFrameworkPackages == false)
                throw new InvalidOperationException("Purging is only viable when framework packages are synchronized!");

            if (options?.PurgePackages == true && _automationInterface != null)
            {
                _logger.Info("Purging packages");
                await PurgePackagesAsync(_config, _automationInterface);
                await ConfigUtils.ProcessModulesAsync(_config, moduleConfig => PurgePackagesAsync(moduleConfig, new AutomationInterfaceHeadless(moduleConfig)));
            }

            version = AutomationInterface.NormalizedVersion(version);
            var artifacts = await SetPackageVersionAsync(_config, _automationInterface, version, options, cancellationToken);
            var artifactsModules = await ConfigUtils.ProcessModulesCollectAsync(_config, moduleConfig => SetPackageVersionAsync(moduleConfig, new AutomationInterfaceHeadless(moduleConfig), version, options, cancellationToken));
            artifacts.AddRange(artifactsModules.SelectMany(x => x));

            // also include all framework packages if required
            if (options?.SyncFrameworkPackages == true)
            {
                // resolve the plcs as packages
                var affectedPackages = artifacts.SelectMany(x => x.AffectedPackages)
                             .GroupBy(x => x.Config.Name)
                             .Select(x => x.FirstOrDefault())
                             .ToList();

                // resolve plcs packages to get dependencies and the framework they are part of
                affectedPackages = await AffectedPackagesAsync(affectedPackages, includeDependencies: false, cancellationToken);

                var frameworks = affectedPackages
                    .Where(x => x.PackageVersion?.Framework != null && artifacts.Any(y => y.ArtifactName == x.PackageVersion?.Name))
                    .Select(x => x.PackageVersion?.Framework).Distinct().ToList();

                // now that we have all involved packages and know if they belong to a framework, seperate them by framework vs. non-framework packages
                var allFrameworkPackages = affectedPackages.Where(x => frameworks.Contains(x.PackageVersion?.Framework)).ToList();
                var allNonFrameworkPackages = affectedPackages.Where(x => !frameworks.Contains(x.PackageVersion?.Framework)).ToList();

                if (allFrameworkPackages.Any())
                    _logger.Info("Synchronizing framework packages");

                foreach (var artifact in artifacts)
                {
                    var packagesToAdd = options?.PurgePackages == true
                        ? artifact.Packages
                        .Where(x => allFrameworkPackages.Any(y => x.Config.Name == y.Config.Name) == false)
                        .Select(
                            x => new PackageItem(x)
                            {
                                Package = new PackageGetResponse(allNonFrameworkPackages.FirstOrDefault(y => y.ProjectName == x.ProjectName && y.PlcName == x.PlcName && x.Config.Name == y.Config.Name)?.Package),
                                PackageVersion = new PackageVersionGetResponse(allNonFrameworkPackages.FirstOrDefault(y => y.ProjectName == x.ProjectName && y.PlcName == x.PlcName && x.Config.Name == y.Config.Name)?.PackageVersion)
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

                    var packagesWithFrameworkToAdd = new List<PackageItem>();
                    var packagesWithFramework = artifact.Packages.Where(x => allFrameworkPackages.Any(y => y.PackageVersion.Name == x.Config.Name)).ToList();

                    foreach (var package in packagesWithFramework)
                    {
                        var affectedPackage = allFrameworkPackages.First(y => y.PackageVersion.Name == package.Config.Name);

                        // check if the requested version is actually on a package server already
                        var requestedPackage =await _packageServers.ResolvePackageAsync(
                            package.Config.Name,
                            new ResolvePackageOptions
                            {
                                PreferredVersion = version,
                                PreferredBranch = options?.PreferredFrameworkBranch,
                                PreferredTarget = options?.PreferredFrameworkTarget,
                                PreferredConfiguration = options?.PreferredFrameworkConfiguration
                            });

                        if (requestedPackage?.Version == version)
                        {
                            package.Config.Version = version;
                            package.Config.Branch = requestedPackage?.Branch;
                            package.Config.Target = requestedPackage?.Target;
                            package.Config.Configuration = requestedPackage?.Configuration;

                            // since the package actually exists, we can add it to the plcproj file
                            packagesToAdd.Add(new PackageItem(affectedPackage) { ProjectName = artifact.Plc.ProjectName, PlcName = artifact.Plc.Name, Package = null, PackageVersion = null, Config = package.Config });
                        }
                        else
                        {
                            package.Config.Version = version;
                            package.Config.Branch = options?.PreferredFrameworkBranch;
                            package.Config.Target = options?.PreferredFrameworkTarget;
                            package.Config.Configuration = options?.PreferredFrameworkConfiguration;

                            // only a headless interface allows to add not existing packages
                            if (_automationInterface is AutomationInterfaceHeadless)
                            {
                                packagesWithFrameworkToAdd.Add(new PackageItem(affectedPackage)
                                {
                                    ProjectName = artifact.Plc.ProjectName,
                                    PlcName = artifact.Plc.Name,
                                    PackageVersion = new PackageVersionGetResponse(affectedPackage.PackageVersion)
                                    {
                                        Version = version,
                                        Branch = package.Config.Branch,
                                        Configuration = package.Config.Configuration,
                                        Target = package.Config.Target
                                    },
                                    Config = package.Config
                                });
                            }
                        }
                    }

                    var automationInterface = artifact.Config == _config ? _automationInterface : new AutomationInterfaceHeadless(artifact.Config);
                    var cache = new List<PackageItem>();
                    await AddPackagesAsync(artifact.Config, automationInterface, _packageServers, cache, packagesToAdd);
                    await AddPackagesAsync(artifact.Config, automationInterface, _packageServers, cache, packagesWithFrameworkToAdd, new AddPackageOptions { SkipDownload = true, IncludeDependencies = false });
                    await automationInterface.SaveAllAsync();
                    ConfigFactory.Save(artifact.Config);
                }
            }
        }
    }
}
