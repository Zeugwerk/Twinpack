using EnvDTE;
using EnvDTE80;
using NLog;
using NLog.Filters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
using Twinpack.Models.Api;
using Twinpack.Protocol;
using static System.Net.Mime.MediaTypeNames;

namespace Twinpack.Core
{
    public class TwinpackService : INotifyPropertyChanged
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private List<PackageItem> _availablePackageCache = new List<PackageItem>();
        private List<PackageItem> _configuredPackagesCache = new List<PackageItem>();

        private PackageServerCollection _packageServers;
        private IAsyncEnumerator<PackageItem> _availablePackagesIt;
        private string _searchTerm;
        private Config _config;
        private IAutomationInterface _automationInterface;

        public event PropertyChangedEventHandler PropertyChanged;

        public TwinpackService(PackageServerCollection packageServers, IAutomationInterface automationInterface=null)
        {
            _packageServers = packageServers;
            _automationInterface = automationInterface;
        }

        public IEnumerable<IPackageServer> PackageServers { get => _packageServers; }
        public bool IsClientUpdateAvailable { get => _packageServers.Any(x => (x as TwinpackServer)?.IsClientUpdateAvailable == true); }
        public bool HasUnknownPackages { get => ConfiguredPackages.Any(x => x.Name == null) == true; }
        public IEnumerable<PackageItem> ConfiguredPackages { get => _configuredPackagesCache; }

        private HashSet<string> CopyLicenseTmcIfNeeded(IEnumerable<PackageItem> packages, HashSet<string> knownLicenseIds)
        {
            // todo: flatten dependences and package versions and iterate over this
            foreach (var package in packages)
            {
                if (package.PackageVersion.HasLicenseTmcBinary)
                {
                    _logger.Trace($"Copying license description file to TwinCAT for {package.PackageVersion.Name} ...");
                    try
                    {
                        var licenseId = ParseLicenseId(package.PackageVersion.LicenseTmcText);
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

                if (package.PackageVersion.Dependencies != null)
                {
                    knownLicenseIds = CopyLicenseTmcIfNeeded(package.PackageVersion.Dependencies.Select(x => new PackageItem() { PackageVersion = x }), knownLicenseIds);
                }
            }

            return knownLicenseIds;
        }

        private static string ParseLicenseId(string content)
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
            _configuredPackagesCache.Clear();
            _availablePackagesIt = null;
            _packageServers.InvalidateCache();
        }

        public async Task LoginAsync(string username=null, string password=null)
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

        public async Task<IEnumerable<PackageItem>> RetrieveConfiguredPackagesAsync(Config config, string searchTerm = null, bool includeMetadata = false, CancellationToken token = default)
        {

            foreach (var project in config.Projects)
            {
                var packages = project.Plcs.SelectMany(x => x.Packages);

                foreach (var package in packages.Where(x => _configuredPackagesCache.Any(y => y.Name == x.Name) == false))
                {
                    PackageItem catalogItem = await _packageServers.ResolvePackageAsync(project.Name, package, includeMetadata, _automationInterface, token);

                    _configuredPackagesCache.RemoveAll(x => !string.IsNullOrEmpty(x.Name) && x.Name == catalogItem.Name);
                    _configuredPackagesCache.Add(catalogItem);

                    if (catalogItem.PackageServer == null)
                        _logger.Warn($"Package {package.Name} {package.Version} (distributor: {package.DistributorName}) referenced in the configuration can not be found on any package server");
                    else
                        _logger.Info($"Package {package.Name} {package.Version} (distributor: {package.DistributorName}) located on {catalogItem.PackageServer.UrlBase}");
                }
            }

            return _configuredPackagesCache
                    .Where(x =>
                        searchTerm == null ||
                        x.DisplayName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.DistributorName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    ;
        }

        public async Task RemovePackageAsync(PackageItem CatalogItem, bool uninstall)
        {
            if (CatalogItem.PackageVersion.Name == null)
                throw new Exception("No packages is selected that could be uninstalled!");

            await _automationInterface.RemovePackageAsync(CatalogItem, uninstall: uninstall);

            // update cache
            var availablePackage = _availablePackageCache.FirstOrDefault(x => x.Name == CatalogItem.PackageVersion.Name);
            if(availablePackage != null)
                availablePackage.Installed = null;
            _configuredPackagesCache.RemoveAll(x => x.Name == CatalogItem.PackageVersion.Name);
        }

        public async Task AddPackageAsync(PackageItem package, bool forceDownload, CancellationToken cancellationToken = default)
        {
            await AddPackagesAsync(new List<PackageItem> { package }, forceDownload, cancellationToken);
        }

        public async Task AddPackagesAsync(List<PackageItem> packages, bool forceDownload, CancellationToken cancellationToken = default)
        {
            if (packages.Any(x => x.PackageVersion.Name == null) == true)
                throw new Exception("Invalid package(s) should be added or updated!");

            var cachePath = $@"{_automationInterface.SolutionPath}\.Zeugwerk\libraries";

            // license handling
            var knownLicenseIds = KnownLicenseIds();
            CopyLicenseTmcIfNeeded(packages, knownLicenseIds);

           // download packages and close Library Manager and all windows that are related to the library. These windows cause race conditions
            var downloadedPackageVersions = new List<PackageItem>();
            foreach (var package in packages)
            {
                downloadedPackageVersions = await DownloadPackageAsync(package, downloadedPackageVersions, forceDownload: forceDownload, cachePath: cachePath, cancellationToken: cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await _automationInterface.RemovePackageAsync(package);
                cancellationToken.ThrowIfCancellationRequested();
            }

            // install packages
            foreach (var package in downloadedPackageVersions)
            {
                await _automationInterface.InstallPackageAsync(package, cachePath: cachePath);
                cancellationToken.ThrowIfCancellationRequested();
            }

            // add references
            foreach (var package in downloadedPackageVersions)
            {
                await _automationInterface.AddPackageAsync(package);
                cancellationToken.ThrowIfCancellationRequested();
            }

            // update cache
            foreach (var package in packages)
            {
                var parameters = package.Config.Parameters;

                _configuredPackagesCache.RemoveAll(x => string.Equals(x.Name, package.PackageVersion.Name, StringComparison.InvariantCultureIgnoreCase));

                var plcConfig = _config.Projects.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs.FirstOrDefault(x => x.Name == package.PlcName);
                var packageIndex = plcConfig.Packages.FindIndex(x => x.Name == package.PackageVersion.Name);

                if (packageIndex >= 0)
                    plcConfig.Packages[packageIndex] = package.Config;
                else
                    plcConfig.Packages.Add(package.Config);

                if (package.Config.Options?.AddDependenciesAsReferences == true)
                {
                    foreach (var dependency in package.PackageVersion.Dependencies ?? new List<PackageVersionGetResponse>())
                    {
                        _configuredPackagesCache.RemoveAll(x => string.Equals(x.Name, dependency.Name, StringComparison.InvariantCultureIgnoreCase));
                        var dependencyParameters = plcConfig.Packages.FirstOrDefault(x => x.Name == dependency.Name)?.Parameters;

                        packageIndex = plcConfig.Packages.FindIndex(x => x.Name == dependency.Name);
                        var dependencyConfig = new ConfigPlcPackage
                        {
                            Name = dependency.Name,
                            Repository = dependency.Repository,
                            Branch = dependency.Branch,
                            Configuration = dependency.Configuration,
                            Target = dependency.Target,
                            Version = dependency.Version,
                            DistributorName = dependency.DistributorName,
                            Options = package.Config.Options,
                            Parameters = dependencyParameters
                        };

                        if (packageIndex >= 0)
                            plcConfig.Packages[packageIndex] = dependencyConfig;
                        else
                            plcConfig.Packages.Add(dependencyConfig);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public HashSet<string> KnownLicenseIds()
        {
            var result = new HashSet<string>();
            if (!Directory.Exists(_automationInterface.LicensesPath))
                return result;

            foreach (var fileName in Directory.GetFiles(_automationInterface.LicensesPath, "*.tmc", SearchOption.AllDirectories))
            {
                try
                {
                    var licenseId = ParseLicenseId(File.ReadAllText(fileName));

                    if (licenseId == null)
                        throw new InvalidDataException("The file {fileName} is not a valid license file!");

                    result.Add(licenseId);
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex);
                }


            }

            return result;
        }

        public async Task ResolvePackageMetadataAsync(PackageItem packageItem, CancellationToken cancellationToken = default)
        {
            await _packageServers.ResolvePackageMetadataAsync(packageItem, cancellationToken);
        }

        public async Task<List<PackageItem>> DownloadPackageAsync(PackageItem package, List<PackageItem> downloadedPackageVersions, bool forceDownload = true, string cachePath = null, CancellationToken cancellationToken = default)
        {
            if (!forceDownload && _automationInterface == null)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            // check if we find the package on the system
            bool referenceFound = !forceDownload && _automationInterface != null && await _automationInterface.IsPackageInstalledAsync(package);

            if ((!referenceFound || forceDownload) && downloadedPackageVersions.Any(x => x.PackageVersion.Name == package.PackageVersion.Name) == false)
            {
                if(await _packageServers.DownloadPackageVersionAsync(package, cachePath, cancellationToken))
                    downloadedPackageVersions.Add(package);
            }

            if (package.PackageVersion.Dependencies != null)
            {
                foreach (var dependency in package.PackageVersion.Dependencies)
                {
                    downloadedPackageVersions = await DownloadPackageAsync(
                        new PackageItem() { PackageVersion = dependency, Config = new ConfigPlcPackage { Options = new AddPlcLibraryOptions { AddDependenciesAsReferences = false } } }, 
                        downloadedPackageVersions, 
                        forceDownload, 
                        cachePath, 
                        cancellationToken: cancellationToken);
                }
            }

            return downloadedPackageVersions;
        }
    }
}
