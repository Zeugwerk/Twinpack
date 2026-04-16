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
using Twinpack.Core;

namespace Twinpack.Application
{
    public partial class TwinpackService : INotifyPropertyChanged
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private SemaphoreSlim _availablePackagesMutex = new SemaphoreSlim(1, 1);
        private List<PackageItem> _availablePackagesCache = new List<PackageItem>();
        private IAsyncEnumerator<PackageItem> _availablePackagesIt;
        private string _searchTerm;

        private SemaphoreSlim _usedPackagesMutex = new SemaphoreSlim(1,1);
        private List<PackageItem> _usedPackagesCache = new List<PackageItem>();

        private IPackageServerCollection _packageServers;
        private Config _config;
        private string _projectName;
        private string _plcName;
        private IAutomationInterface _automationInterface;

        public event PropertyChangedEventHandler PropertyChanged;

        public TwinpackService(IPackageServerCollection packageServers, IAutomationInterface automationInterface=null, Config config=null, string projectName=null, string plcName=null)
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
                await _usedPackagesMutex.WaitAsync(token);

                var workItems = new List<(string ProjectName, string PlcName, PlcPackageReference Package)>();
                var workItemKeys = new HashSet<(string ProjectName, string PlcName, PlcPackageReference Package)>();
                foreach (var project in _config.Projects.Where(x => x.Name == _projectName || _projectName == null))
                {
                    foreach (var plc in project.Plcs.Where(x => x.Name == _plcName || _plcName == null))
                    {
                        foreach (var package in plc.Packages
                            .Where(x => excludedPackages == null || !excludedPackages.Contains(x.Name))
                            .Where(x => !_usedPackagesCache.Any(y => y.ProjectName == project.Name && y.PlcName == plc.Name && y.Catalog?.Name == x.Name)))
                        {
                            var key = (project.Name, plc.Name, package);
                            if (!workItemKeys.Add(key))
                                continue;

                            workItems.Add(key);
                        }
                    }
                }

                if (workItems.Count > 0)
                {
                    const int maxConcurrent = 8;
                    using (var throttle = new SemaphoreSlim(maxConcurrent, maxConcurrent))
                    {
                        var fetchTasks = new Task<(int index, string projectName, string plcName, PlcPackageReference package, PackageItem catalogItem)>[workItems.Count];
                        for (var i = 0; i < workItems.Count; i++)
                        {
                            var index = i;
                            var item = workItems[i];
                            fetchTasks[i] = Task.Run(async () =>
                            {
                                await throttle.WaitAsync(token).ConfigureAwait(false);
                                try
                                {
                                    token.ThrowIfCancellationRequested();
                                    var catalogItem = await _packageServers.FetchPackageAsync(item.ProjectName, item.PlcName, item.Package, includeMetadata, _automationInterface, token).ConfigureAwait(false);
                                    return (index, item.ProjectName, item.PlcName, item.Package, catalogItem);
                                }
                                finally
                                {
                                    throttle.Release();
                                }
                            }, token);
                        }

                        var fetched = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
                        foreach (var row in fetched.OrderBy(x => x.index))
                        {
                            var catalogItem = row.catalogItem;
                            var package = row.package;

                            _usedPackagesCache.RemoveAll(x => x.ProjectName == row.projectName && x.PlcName == row.plcName && !string.IsNullOrEmpty(x.Catalog?.Name) && x.Catalog?.Name == catalogItem.Catalog?.Name);
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
                if (package.PlcPackageReference.Name == null)
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
                if (package.PlcPackageReference.Name == null)
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

            var packages = usedPackages.Select(x =>
            {
                var p = new PackageItem(x);
                if (x.Used != null)
                    p.Apply(new ResolvedPackageRef(x.Used, x.Used));
                return p;
            }).ToList();
            var providedPackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.PlcPackageReference.Name) == true).ToList(), new AddPackageOptions(options) { SkipDownload = !options.IncludeProvidedPackages }, cancellationToken: cancellationToken);

            var installablePackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.PlcPackageReference.Name) == false).ToList(), options, cancellationToken: cancellationToken);
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
                    .Select(x =>
                    {
                        var p = new PackageItem(x);
                        if (x.Update != null)
                            p.Apply(new ResolvedPackageRef(x.Update, new Protocol.Api.PublishedPackage(x.Update)));
                        return p;
                    }).ToList();

                foreach (var package in packages)
                {
                    var i = filters.Packages != null && package.Package.Name != null ? Array.IndexOf(filters.Packages, package.Package.Name) : -1;
                    if (i >= 0)
                    {
                        package.PlcPackageReference.Version = string.IsNullOrEmpty(filters.Versions?.ElementAtOrDefault(i)) ? null : filters.Versions?.ElementAtOrDefault(i);
                        package.PlcPackageReference.Branch = filters.Branches?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.PlcPackageReference.Version) ? package.PlcPackageReference.Branch : null);
                        package.PlcPackageReference.Configuration = filters.Configurations?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.PlcPackageReference.Version) ? package.PlcPackageReference.Configuration : null);
                        package.PlcPackageReference.Target = filters.Targets?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.PlcPackageReference.Version) ? package.PlcPackageReference.Target : null);
                    }

                    i = filters.Frameworks != null && package.PackageVersion.Framework != null ? Array.IndexOf(filters.Frameworks, package.PackageVersion.Framework) : -1;
                    if (i >= 0)
                    {
                        package.PlcPackageReference.Version = filters.Versions?.ElementAtOrDefault(i);
                        package.PlcPackageReference.Branch = filters.Branches?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.PlcPackageReference.Version) ? package.PlcPackageReference.Branch : null);
                        package.PlcPackageReference.Configuration = filters.Configurations?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.PlcPackageReference.Version) ? package.PlcPackageReference.Configuration : null);
                        package.PlcPackageReference.Target = filters.Targets?.ElementAtOrDefault(i) ?? (string.IsNullOrEmpty(package.PlcPackageReference.Version) ? package.PlcPackageReference.Target : null);
                    }
                }

                // force new retrievable of metadata
                foreach (var package in packages)
                    package.Apply((ResolvedPackageRef?)null);
            }
            else
            {
                packages = usedPackages.Select(x =>
                {
                    var p = new PackageItem(x);
                    if (x.Update != null)
                        p.Apply(new ResolvedPackageRef(x.Update, new Protocol.Api.PublishedPackage(x.Update)));
                    return p;
                }).ToList();
            }

            return await UpdatePackagesAsync(packages, options, cancellationToken);
        }

        protected async System.Threading.Tasks.Task<List<PackageItem>> UpdatePackagesAsync(List<PackageItem> packages, UpdatePackageOptions options = default, CancellationToken cancellationToken = default)
        {
            if (_config.Modules?.Any() == true)
                throw new NotSupportedException("Modules are not supported");

            var usedPackages = await RetrieveUsedPackagesAsync(token: cancellationToken);
            if (packages.Any(x => string.IsNullOrEmpty(x.PlcPackageReference?.Name) || !usedPackages.Any(y => PackageItemReference.SameConfiguredReference(x, y))))
                throw new ArgumentException("Package to be updated is not used in any project!");

            // download and add all packages, which are not self references to the provided packages
            var providedPackageNames = _config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();
            var providedPackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.PlcPackageReference.Name) == true).ToList(), new AddPackageOptions(options) { SkipDownload = !options.IncludeProvidedPackages }, cancellationToken: cancellationToken);

            var installablePackages = await AddPackagesAsync(packages.Where(x => providedPackageNames.Any(y => y == x.PlcPackageReference.Name) == false).ToList(), options, cancellationToken: cancellationToken);
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
            return await AddPackagesAsync(new PackageOperationContext(_config, _automationInterface, _packageServers, cancellationToken), _usedPackagesCache, packages, options);
        }

        private static async Task<List<PackageItem>> AddPackagesAsync(PackageOperationContext ctx, List<PackageItem> cache, List<PackageItem> packages, AddPackageOptions options = default)
        {
            if (ctx.Config.Modules?.Any() == true)
                throw new NotSupportedException("Modules are not supported");

            var addedPackages = new List<PackageItem>();
            if (packages.Any(x => x.PlcPackageReference.Name == null) == true)
                throw new Exception("Invalid package(s) should be added or updated!");

            var affectedPackages = await AffectedPackagesAsync(ctx, packages, new List<PackageItem>(), includeDependencies: true);
            var downloadPath = options?.DownloadPath ?? Path.Combine(ctx.AutomationInterface?.SolutionPath ?? ".", ".Zeugwerk", "libraries");

            // copy runtime licenses
            CopyRuntimeLicenseIfNeeded(ctx.AutomationInterface, affectedPackages);

            var closeAllPackageRelatedWindowsTask = ctx.AutomationInterface?.CloseAllPackageRelatedWindowsAsync(affectedPackages) ?? Task.Delay(new TimeSpan(0));
            var downloadPackagesTask = options?.SkipDownload == true
                ? Task.Run(() => new List<PackageItem>())
                : DownloadPackagesAsync(ctx, affectedPackages,
                new DownloadPackageOptions
                {
                    IncludeProvidedPackages = true,
                    IncludeDependencies = options?.IncludeDependencies == true,
                    ForceDownload = options?.ForceDownload == true,
                    DownloadPath = downloadPath
                });
            await Task.WhenAll(closeAllPackageRelatedWindowsTask, downloadPackagesTask);

            var downloadedPackageVersions = await downloadPackagesTask;

            // install downloaded packages
            if (ctx.AutomationInterface != null && !(ctx.AutomationInterface is AutomationInterfaceHeadless) &&
                (options?.SkipInstall == null || options?.SkipInstall == false))
            {
                foreach (var package in downloadedPackageVersions)
                {
                    _logger.Info($"Installing {package.PackageVersion.Name} {package.PackageVersion.Version}");
                    await ctx.AutomationInterface.InstallPackageAsync(package, cachePath: downloadPath);
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                }
            }

            // add affected packages as references
            foreach (var package in (options?.IncludeDependencies == true ? affectedPackages : packages).Where(x => x.PackageVersion?.Name != null))
            {
                _logger.Info($"Adding {package.PackageVersion.Name} {package.PackageVersion.Version} (distributor: {package.PackageVersion.DistributorName}) to {package.ProjectName}/{package.PlcName}");

                if (ctx.AutomationInterface != null && (options?.UpdatePlc == null || options?.UpdatePlc == true))
                {
                    await ctx.AutomationInterface.AddPackageAsync(package);
                }

                var parameters = package.PlcPackageReference.Parameters;

                // delete from package cache so the pac
                cache.RemoveAll(x => string.Equals(x.Catalog?.Name, package.PackageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                // update configuration
                var plcConfig = ctx.Config?.Projects.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
                var packageIndex = plcConfig?.Packages.FindIndex(x => x.Name == package.PackageVersion.Name);
                var newPackageConfig = PlcPackageReference.PersistAfterResolve(package);

                package.Apply(new ConfiguredPackageRef(package.ProjectName, package.PlcName, newPackageConfig));
                addedPackages.Add(package);

                if (packageIndex.HasValue && packageIndex.Value >= 0)
                    plcConfig.Packages[packageIndex.Value] = newPackageConfig;
                else
                    plcConfig.Packages.Add(newPackageConfig);

                ctx.CancellationToken.ThrowIfCancellationRequested();
            }

            if (ctx.AutomationInterface != null)
                await ctx.AutomationInterface.SaveAllAsync();

            ConfigFactory.Save(ctx.Config);

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
            var resolvedPackage = await _packageServers.FetchPackageAsync(packageItem.PackageServer, packageItem.ProjectName, packageItem.PlcName, packageItem.PlcPackageReference ?? new PlcPackageReference(packageItem), includeMetadata: true, automationInterface: _automationInterface, cancellationToken: cancellationToken);
            packageItem.PlcPackageReference ??= resolvedPackage.PlcPackageReference;
            if (resolvedPackage.GetResolvedPackageRef() is { } resolvedRef)
                packageItem.Apply(resolvedRef);
            packageItem.PackageServer ??= resolvedPackage.PackageServer;
        }

        public async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, bool includeDependencies = true, CancellationToken cancellationToken = default)
        {
            return await AffectedPackagesAsync(packages, new List<PackageItem>(), includeDependencies, cancellationToken);
        }

        public async Task<List<PackageItem>> AffectedPackagesAsync(List<PackageItem> packages, List<PackageItem> cache, bool includeDependencies = true, CancellationToken cancellationToken = default)
        {
            return await AffectedPackagesAsync(new PackageOperationContext(_config, _automationInterface, _packageServers, cancellationToken), packages, cache, includeDependencies);
        }

        private static Task<List<PackageItem>> AffectedPackagesAsync(PackageOperationContext ctx, List<PackageItem> packages, List<PackageItem> cache, bool includeDependencies = true)
        {
            return AffectedPackagesClosureBuilder.BuildAsync(
                ctx.AutomationInterface,
                ctx.PackageServers,
                packages,
                cache,
                includeDependencies,
                ctx.CancellationToken);
        }

        public async Task<List<PackageItem>> DownloadPackagesAsync(List<PackageItem> packages, DownloadPackageOptions options = default, CancellationToken cancellationToken = default)
        {
            return await DownloadPackagesAsync(new PackageOperationContext(_config, _automationInterface, _packageServers, cancellationToken), packages, options);
        }

        private static async Task<List<PackageItem>> DownloadPackagesAsync(PackageOperationContext ctx, List<PackageItem> packages, DownloadPackageOptions options = default)
        {
            List<PackageItem> downloadedPackages = new List<PackageItem> { };
            List<PackageItem> affectedPackages = packages.ToList();

            if (options.IncludeDependencies)
                affectedPackages = await AffectedPackagesAsync(ctx, affectedPackages, new List<PackageItem>(), includeDependencies: true);

            // avoid downloading duplicates
            affectedPackages = affectedPackages.GroupBy(x => new
            {
                x.PackageVersion.Name,
                x.PackageVersion.Version,
                x.PackageVersion.Branch,
                x.PackageVersion.Target,
                x.PackageVersion.Configuration
            }).Select(x => x.First()).ToList();

            if (!options.ForceDownload && ctx.AutomationInterface == null)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            // ignore packages, which are provided by the loaded configuration
            if (!options.IncludeProvidedPackages)
            {
                var providedPackageNames = ctx.Config?.Projects?.SelectMany(x => x.Plcs).Select(x => x.Name).ToList() ?? new List<string>();
                affectedPackages = affectedPackages.Where(x => providedPackageNames.Any(y => y == x.PackageVersion.Name) == false).ToList();
            }

            foreach (var affectedPackage in affectedPackages)
            {
                // check if we find the package on the system
                bool referenceFound = !options.ForceDownload && ctx.AutomationInterface != null && await ctx.AutomationInterface.IsPackageInstalledAsync(affectedPackage);

                if (!referenceFound || options.ForceDownload)
                {
                    if (await ctx.PackageServers.DownloadPackageVersionAsync(affectedPackage, options.DownloadPath, ctx.CancellationToken))
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

        private async Task<List<VersionedPlcProjectScope>> SetPackageVersionAsync(Config config, IAutomationInterface automationInterface, string version, SetPackageVersionOptions options, CancellationToken cancellationToken)
        {
            // set the version of all plcs in the project(s)
            var plcs = config.Projects
                .Where(x => options?.ProjectName == null || x.Name == options?.ProjectName)
                .SelectMany(x => x.Plcs)
                .Where(x => options?.PlcName == null || x.Name == options?.PlcName)
                ;

            List<VersionedPlcProjectScope> versionScopes = new List<VersionedPlcProjectScope>();
            foreach (var plc in plcs)
            {
                _logger.Info(new string('-', 3) + $" set-version:{plc.Name}");

                plc.Version = version;

                if (_automationInterface != null)
                    await automationInterface.SetPackageVersionAsync(plc, cancellationToken);

                versionScopes.Add(new VersionedPlcProjectScope(config, plc));
            }

            if (_automationInterface != null)
                await automationInterface.SaveAllAsync();

            ConfigFactory.Save(config);

            return versionScopes;
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
            var versionScopes = await SetPackageVersionAsync(_config, _automationInterface, version, options, cancellationToken);
            var moduleVersionScopes = await ConfigUtils.ProcessModulesCollectAsync(_config, moduleConfig => SetPackageVersionAsync(moduleConfig, new AutomationInterfaceHeadless(moduleConfig), version, options, cancellationToken));
            versionScopes.AddRange(moduleVersionScopes.SelectMany(x => x));

            // also include all framework packages if required
            if (options?.SyncFrameworkPackages == true)
            {
                // resolve the plcs as packages
                var affectedPackages = versionScopes.SelectMany(x => x.AffectedPackages)
                             .GroupBy(x => x.PlcPackageReference.Name)
                             .Select(x => x.FirstOrDefault())
                             .ToList();

                // resolve plcs packages to get dependencies and the framework they are part of
                affectedPackages = await AffectedPackagesAsync(affectedPackages, includeDependencies: false, cancellationToken);

                var frameworks = affectedPackages
                    .Where(x => x.PackageVersion?.Framework != null && versionScopes.Any(y => y.PlcAsPackageName == x.PackageVersion?.Name))
                    .Select(x => x.PackageVersion?.Framework).Distinct().ToList();

                // now that we have all involved packages and know if they belong to a framework, seperate them by framework vs. non-framework packages
                var allFrameworkPackages = affectedPackages.Where(x => frameworks.Contains(x.PackageVersion?.Framework)).ToList();
                var allNonFrameworkPackages = affectedPackages.Where(x => !frameworks.Contains(x.PackageVersion?.Framework)).ToList();

                if (allFrameworkPackages.Any())
                    _logger.Info("Synchronizing framework packages");

                foreach (var scope in versionScopes)
                {
                    var packagesToAdd = options?.PurgePackages == true
                        ? scope.Packages
                        .Where(x => allFrameworkPackages.Any(y => x.PlcPackageReference.Name == y.PlcPackageReference.Name) == false)
                        .Select(
                            x => new PackageItem(x)
                            {
                                Package = new PublishedPackage(allNonFrameworkPackages.FirstOrDefault(y => y.ProjectName == x.ProjectName && y.PlcName == x.PlcName && x.PlcPackageReference.Name == y.PlcPackageReference.Name)?.Package),
                                PackageVersion = new PublishedPackageVersion(allNonFrameworkPackages.FirstOrDefault(y => y.ProjectName == x.ProjectName && y.PlcName == x.PlcName && x.PlcPackageReference.Name == y.PlcPackageReference.Name)?.PackageVersion)
                                {
                                    Title = x.PackageVersion?.Title ?? x.PlcPackageReference.Name,
                                    Name = x.PlcPackageReference.Name,
                                    DistributorName = x.PlcPackageReference.DistributorName,
                                    Version = x.PlcPackageReference.Version,
                                    Branch = x.PlcPackageReference.Branch,
                                    Target = x.PlcPackageReference.Target,
                                    Configuration = x.PlcPackageReference.Configuration,
                                }
                            }
                        )
                        .ToList()
                      : new List<PackageItem>();

                    var packagesWithFrameworkToAdd = new List<PackageItem>();
                    var packagesWithFramework = scope.Packages.Where(x => allFrameworkPackages.Any(y => y.PackageVersion.Name == x.PlcPackageReference.Name)).ToList();

                    foreach (var package in packagesWithFramework)
                    {
                        var affectedPackage = allFrameworkPackages.First(y => y.PackageVersion.Name == package.PlcPackageReference.Name);

                        // check if the requested version is actually on a package server already
                        var requestedPackage =await _packageServers.ResolvePackageAsync(
                            package.PlcPackageReference.Name,
                            new ResolvePackageOptions
                            {
                                PreferredVersion = version,
                                PreferredBranch = options?.PreferredFrameworkBranch,
                                PreferredTarget = options?.PreferredFrameworkTarget,
                                PreferredConfiguration = options?.PreferredFrameworkConfiguration
                            });

                        if (requestedPackage?.Version == version)
                        {
                            package.PlcPackageReference.Version = version;
                            package.PlcPackageReference.Branch = requestedPackage?.Branch;
                            package.PlcPackageReference.Target = requestedPackage?.Target;
                            package.PlcPackageReference.Configuration = requestedPackage?.Configuration;

                            // since the package actually exists, we can add it to the plcproj file
                            var headlessAdd = new PackageItem(affectedPackage);
                            headlessAdd.Apply(new ConfiguredPackageRef(scope.Plc.ProjectName, scope.Plc.Name, package.PlcPackageReference));
                            headlessAdd.Apply((ResolvedPackageRef?)null);
                            packagesToAdd.Add(headlessAdd);
                        }
                        else
                        {
                            package.PlcPackageReference.Version = version;
                            package.PlcPackageReference.Branch = options?.PreferredFrameworkBranch;
                            package.PlcPackageReference.Target = options?.PreferredFrameworkTarget;
                            package.PlcPackageReference.Configuration = options?.PreferredFrameworkConfiguration;

                            // only a headless interface allows to add not existing packages
                            if (_automationInterface is AutomationInterfaceHeadless)
                            {
                                var frameworkAdd = new PackageItem(affectedPackage);
                                frameworkAdd.Apply(new ConfiguredPackageRef(scope.Plc.ProjectName, scope.Plc.Name, package.PlcPackageReference));
                                frameworkAdd.Apply(new ResolvedPackageRef(
                                    new PublishedPackageVersion(affectedPackage.PackageVersion)
                                    {
                                        Version = version,
                                        Branch = package.PlcPackageReference.Branch,
                                        Configuration = package.PlcPackageReference.Configuration,
                                        Target = package.PlcPackageReference.Target
                                    },
                                    null));
                                packagesWithFrameworkToAdd.Add(frameworkAdd);
                            }
                        }
                    }

                    var automationInterface = scope.Config == _config ? _automationInterface : new AutomationInterfaceHeadless(scope.Config);
                    var cache = new List<PackageItem>();
                    var moduleCtx = new PackageOperationContext(scope.Config, automationInterface, _packageServers, cancellationToken);
                    await AddPackagesAsync(moduleCtx, cache, packagesToAdd);
                    await AddPackagesAsync(moduleCtx, cache, packagesWithFrameworkToAdd, new AddPackageOptions { SkipDownload = true, IncludeDependencies = false });
                    await automationInterface.SaveAllAsync();
                    ConfigFactory.Save(scope.Config);
                }
            }
        }
    }
}
