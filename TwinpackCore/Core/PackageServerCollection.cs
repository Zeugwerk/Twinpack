using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Exceptions;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using Twinpack.Protocol;
using Twinpack.Configuration;
using System.Runtime.Caching;

namespace Twinpack.Core
{
    public class PackageServerCollection : List<IPackageServer>
    {
        private class ResolvedDependenciesResult
        {
            public List<PackageItem> Immediate { get; } = new List<PackageItem>();
            public List<PackageItem> Flat { get; } = new List<PackageItem>();
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static MemoryCache _cache = MemoryCache.Default;

        public void InvalidateCache()
        {
            ForEach(x => x.InvalidateCache());

            try
            {
                foreach (var item in _cache)
                    _cache.Remove(item.Key);
            }
            finally
            {
            }
        }

        public async Task LoginAsync(string username, string password)
        {
            foreach (var packageServer in this)
            {
                try
                {
                    await packageServer.LoginAsync(username, password);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);
                    _logger.Trace(ex);
                }
            }
        }

        public async IAsyncEnumerable<PackageItem> SearchAsync(string filter=null, int? maxPackages=null, int batchSize=5, CancellationToken token = default)
        {
            var cache = new HashSet<string>();
            foreach(var packageServer in this.Where(x => x.Connected))
            {
                var page = 1;
                Tuple<IEnumerable<CatalogItemGetResponse>, bool> packages;
                do
                {
                    packages = await packageServer.GetCatalogAsync(filter, page, batchSize, token);
                    foreach (var package in packages.Item1.Where(x => !cache.Contains(x.Name)))
                    {
                        cache.Add(package.Name);
                        yield return new PackageItem(packageServer, package);
                        if (maxPackages != null && cache.Count >= maxPackages)
                            yield break;
                    }

                    page++;
                } while (packages?.Item2 == true);
            }
        }

        public async Task<PackageVersionGetResponse> ResolvePackageAsync(string name, TwinpackService.ResolvePackageOptions options = default, CancellationToken cancellationToken = default)
        {
            foreach (var packageServer in this.Where(x => x.Connected))
            {
                var resolvedPackageVersion = await packageServer.ResolvePackageVersionAsync(
                    new PlcLibrary { Name = name, Version = options.PreferredVersion },
                    options.PreferredTarget,
                    options.PreferredConfiguration,
                    options.PreferredBranch,
                    cancellationToken: cancellationToken);

                if (resolvedPackageVersion?.Name != null)
                    return resolvedPackageVersion;
            }

            return new PackageVersionGetResponse();
        }

        public async Task<PackageItem> FetchPackageAsync(ConfigPlcPackage item, bool includeMetadata = false, IAutomationInterface automationInterface = null, bool preferEffectiveVersionForWildcard = false, CancellationToken cancellationToken = default)
        {
            return await FetchPackageAsync(null, null, null, item, includeMetadata, automationInterface, preferEffectiveVersionForWildcard, cancellationToken);
        }

        public async Task<PackageItem> FetchPackageAsync(string projectName, string plcName, ConfigPlcPackage item, bool includeMetadata = false, IAutomationInterface automationInterface = null, bool preferEffectiveVersionForWildcard = false, CancellationToken cancellationToken = default)
        {
            return await FetchPackageAsync(null, projectName, plcName, item, includeMetadata, automationInterface, preferEffectiveVersionForWildcard, cancellationToken);
        }

        public async Task<PackageItem> FetchPackageAsync(IPackageServer packageServer, string projectName, string plcName, ConfigPlcPackage item, bool includeMetadata = false, IAutomationInterface automationInterface=null, bool preferEffectiveVersionForWildcard = false, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"FetchPackageAsync-{projectName}-{plcName}-{item.DistributorName}-{item.Name}-{item.Version}-{item.Branch}-{item.Configuration}-{item.Target}";
            if (_cache.Contains(cacheKey))
                return _cache[cacheKey] as PackageItem;

            var catalogItem = new PackageItem(item)
            {
                ProjectName = projectName,
                PlcName = plcName
            };


            foreach (var ps in packageServer != null ? new List<IPackageServer> { packageServer } : this.Where(x => x.Connected))
            {
                catalogItem.PackageServer = ps;

                // if some data is not present, try to resolve the information
                PackageVersionGetResponse resolvedPackageVersion = await ps.ResolvePackageVersionAsync(
                        new PlcLibrary { Name = item.Name, DistributorName = item.DistributorName, Version = item.Version },
                        item.Target,
                        item.Configuration,
                        item.Branch,
                        cancellationToken: cancellationToken);

                if (resolvedPackageVersion?.Name != null && (item.Branch == null || item.Configuration == null || item.Target == null || item.DistributorName == null))
                {
                    item.Branch = resolvedPackageVersion?.Branch;
                    item.Configuration = resolvedPackageVersion?.Configuration;
                    item.Target = resolvedPackageVersion?.Target;
                    item.DistributorName = resolvedPackageVersion?.DistributorName;
                }

                if (resolvedPackageVersion == null)
                    continue;

                // try to get the installed package, if we can't find it at least try to resolve it
                PackageVersionGetResponse packageVersion = resolvedPackageVersion;
                
                if (packageVersion?.Branch != item.Branch 
                    || packageVersion?.Configuration != item.Configuration
                    || packageVersion?.Target != item.Target
                    || packageVersion?.Version != (item.Version ?? resolvedPackageVersion.Latest?.Version))
                {
                    packageVersion = await ps.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name, Version = item.Version },
                                                              item.Branch, item.Configuration, item.Target,
                                                              cancellationToken: cancellationToken);
                }


                if (preferEffectiveVersionForWildcard && packageVersion?.Name != null && item.Version == null && projectName != null && plcName != null)
                {
                    if (automationInterface != null)
                    {
                        var effectiveVersion = await automationInterface.ResolveEffectiveVersionAsync(projectName, plcName, packageVersion.Title);
                        var effectivePackageVersion = packageVersion;

                        if (effectivePackageVersion?.Branch != item.Branch
                            || effectivePackageVersion?.Configuration != item.Configuration
                            || effectivePackageVersion?.Target != item.Target
                            || effectivePackageVersion?.Version != (item.Version ?? resolvedPackageVersion.Latest?.Version))
                        {

                            effectivePackageVersion = await ps.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name, Version = effectiveVersion },
                                                                                          item.Branch, item.Configuration, item.Target,
                                                                                          cancellationToken: cancellationToken);
                        }

                        if (effectivePackageVersion?.Name != null)
                            packageVersion = effectivePackageVersion;
                        else
                            _logger.Warn("[resolve] package {0} {1}* not available", packageVersion?.Name, effectiveVersion);
                    }
                    else
                    {
                        _logger.Warn("[resolve] cannot resolve wildcard '{0}' without automation interface", packageVersion.Name);
                    }
                }

                var packageVersionLatest = resolvedPackageVersion.Latest;
                if (packageVersionLatest == null)
                {
                    packageVersionLatest = await ps.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name },
                                                                                  item.Branch, item.Configuration, item.Target,
                                                                                  cancellationToken: cancellationToken);
                }

                // force the packageVersion references version even if the version was not found
                if (packageVersion?.Name != null)
                {
                    catalogItem = new PackageItem(ps, packageVersion);
                    catalogItem.Used = packageVersion;
                    catalogItem.Config = item;
                    catalogItem.ProjectName = projectName;
                    catalogItem.PlcName = plcName;

                    if (includeMetadata)
                    {
                        catalogItem.PackageVersion = packageVersion;
                        var resolvedDependencies = await ResolvePackageDependenciesAsync(catalogItem, automationInterface, cancellationToken);
                        catalogItem.Dependencies = resolvedDependencies.Flat;
                        catalogItem.PackageVersion.Dependencies = resolvedDependencies.Immediate.Select(x => x.PackageVersion).ToList();
                    }
                }

                // a package might be updateable but not available on Twinpack
                if (packageVersionLatest.Name != null)
                {
                    if (includeMetadata)
                    {
                        catalogItem.Package = await ps.GetPackageAsync(packageVersionLatest.DistributorName, packageVersionLatest.Name, cancellationToken: cancellationToken);
                    }

                    catalogItem.Update = packageVersionLatest;
                    catalogItem.PackageServer = ps;
                }

                catalogItem.Config = item;

                if (packageVersionLatest.Name != null)
                {
                    if(includeMetadata)
                        _cache[cacheKey] = catalogItem;
                    return catalogItem;
                }
            }

            catalogItem.Config = item;
            catalogItem.ProjectName = projectName;
            catalogItem.PlcName = plcName;
            catalogItem.PackageServer = null;

            if (includeMetadata)
                _cache[cacheKey] = catalogItem;
            return catalogItem;
        }

        /// <summary>
        /// Loads catalog metadata and resolves the dependency graph for a package that already has
        /// <see cref="PackageItem.PackageServer"/> and <see cref="PackageItem.PackageVersion"/> set
        /// (for example the update target version), without re-resolving the version from <see cref="PackageItem.Config"/>.
        /// </summary>
        public async Task PopulateMetadataAndDependenciesAsync(PackageItem package, IAutomationInterface automationInterface = null, CancellationToken cancellationToken = default)
        {
            if (package?.PackageServer == null || package.PackageVersion?.Name == null)
                return;

            var ps = package.PackageServer;
            var v = package.PackageVersion;

            if (v.Dependencies == null || v.Dependencies.Count == 0)
            {
                var full = await ps.GetPackageVersionAsync(
                    new PlcLibrary { DistributorName = v.DistributorName, Name = v.Name, Version = v.Version },
                    v.Branch ?? package.Config?.Branch,
                    v.Configuration ?? package.Config?.Configuration,
                    v.Target ?? package.Config?.Target,
                    cancellationToken: cancellationToken);

                if (full?.Name != null)
                {
                    package.PackageVersion = full;
                    v = full;
                }
            }

            if (package.Package == null)
                package.Package = await ps.GetPackageAsync(v.DistributorName, v.Name, cancellationToken: cancellationToken);

            var resolvedDependencies = await ResolvePackageDependenciesAsync(package, automationInterface, cancellationToken);
            package.Dependencies = resolvedDependencies.Flat;
            package.PackageVersion.Dependencies = resolvedDependencies.Immediate.Select(x => x.PackageVersion).ToList();
        }

        public async Task PullAsync(Config config, bool skipInternalPackages = false, IEnumerable<ConfigPlcPackage> filter = null, string cachePath = null, CancellationToken cancellationToken = default)
        {
            _logger.Info("[pull] from package server(s) (skip internal: {0})", skipInternalPackages);
            var plcs = config.Projects.SelectMany(x => x.Plcs);
            var exceptions = new List<Exception>();
            var handled = new List<ConfigPlcPackage>();
            var lastPackageServer = this.Last();
            foreach(var packageServer in this)
            {
                foreach (var plc in plcs)
                {
                    // skip packages that are provided according to the config file
                    if (skipInternalPackages)
                    {
                        handled.Add(new ConfigPlcPackage
                        {
                            Name = plc.Name,
                            Version = plc.Version,
                            DistributorName = plc.DistributorName,
                            Target = null,
                            Configuration = null,
                            Branch = null
                        });
                    }

                    foreach (var package in plc.Packages ?? new List<ConfigPlcPackage>())
                    {
                        if (handled.Any(x => x?.Name == package?.Name && x?.Version == package?.Version &&
                                        (x.Target == null || x?.Target == package?.Target) &&
                                        (x.Configuration == null || x?.Configuration == package?.Configuration) &&
                                        (x.Branch == null || x?.Branch == package?.Branch)))
                            continue;

                        if (filter != null && !filter.Any(x => x?.Name == package?.Name && x?.Version == package?.Version &&
                                        (x.Target == null || x?.Target == package?.Target) &&
                                        (x.Configuration == null || x?.Configuration == package?.Configuration) &&
                                        (x.Branch == null || x?.Branch == package?.Branch)))
                            continue;

                        try
                        {
                            var packageVersion = await packageServer.GetPackageVersionAsync(new PlcLibrary { DistributorName = package.DistributorName, Name = package.Name, Version = package.Version }, package.Branch, package.Configuration, package.Target);
                            handled.Add(package);

                            if (packageVersion?.Name != null)
                            {
                                await packageServer.DownloadPackageVersionAsync(packageVersion, checksumMode: ChecksumMode.IgnoreMismatch, downloadPath: cachePath, cancellationToken: cancellationToken);

                                
                            }
                            else if (packageServer == this.Last())
                            {
                                throw new ProtocolException($"Package not available on any package server");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn("[pull] {0} {1}: {2}", package.Name, package.Version, ex.Message);
                            exceptions.Add(ex);
                        }
                    }
                }
            }


            if (exceptions.Any())
            {
                throw new ProtocolException($"Pulling for Package Server(s) failed for {exceptions.Count()} dependencies!");
            }
        }

        private async Task<ResolvedDependenciesResult> ResolvePackageDependenciesAsync(PackageItem package, IAutomationInterface automationInterface, CancellationToken cancellationToken = default)
        {
            var resolvedDependencies = new ResolvedDependenciesResult();
            var visitedDependencyKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var addedDependencyKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var dependency in package?.PackageVersion?.Dependencies ?? new List<PackageVersionGetResponse>())
            {
                var immediateDependency = await ResolveDependencyReferenceAsync(package, dependency, automationInterface, cancellationToken);
                if (immediateDependency?.PackageVersion?.Name == null)
                    continue;

                var immediateDependencyKey = BuildDependencyKey(
                    immediateDependency.PackageVersion.DistributorName,
                    immediateDependency.PackageVersion.Name,
                    immediateDependency.PackageVersion.Version,
                    immediateDependency.PackageVersion.Branch,
                    immediateDependency.PackageVersion.Configuration,
                    immediateDependency.PackageVersion.Target);

                if (addedDependencyKeys.Add(immediateDependencyKey))
                {
                    resolvedDependencies.Immediate.Add(immediateDependency);
                    resolvedDependencies.Flat.Add(immediateDependency);
                }

                await ResolvePackageDependencyRecursiveAsync(immediateDependency, automationInterface, resolvedDependencies.Flat, visitedDependencyKeys, addedDependencyKeys, cancellationToken);
            }

            return resolvedDependencies;
        }

        private async Task ResolvePackageDependencyRecursiveAsync(PackageItem parentDependency, IAutomationInterface automationInterface, List<PackageItem> resolvedDependencies, HashSet<string> visitedDependencyKeys, HashSet<string> addedDependencyKeys, CancellationToken cancellationToken)
        {
            foreach (var dependency in parentDependency?.Dependencies ?? new List<PackageItem>())
            {
                if (dependency?.Config?.Name == null)
                    continue;

                var requestedDependencyKey = BuildDependencyKey(dependency.Config);
                if (!visitedDependencyKeys.Add(requestedDependencyKey))
                    continue;

                var dependencyRequest = new PackageVersionGetResponse
                {
                    Name = dependency.Config.Name,
                    DistributorName = dependency.Config.DistributorName,
                    Version = dependency.Config.Version,
                    Branch = dependency.Config.Branch,
                    Configuration = dependency.Config.Configuration,
                    Target = dependency.Config.Target
                };

                var resolvedDependency = await ResolveDependencyReferenceAsync(parentDependency, dependencyRequest, automationInterface, cancellationToken);
                if (resolvedDependency?.PackageVersion?.Name == null)
                    continue;

                var resolvedDependencyKey = BuildDependencyKey(resolvedDependency.PackageVersion);

                if (addedDependencyKeys.Add(resolvedDependencyKey))
                    resolvedDependencies.Add(resolvedDependency);

                await ResolvePackageDependencyRecursiveAsync(resolvedDependency, automationInterface, resolvedDependencies, visitedDependencyKeys, addedDependencyKeys, cancellationToken);
            }
        }

        private async Task<PackageItem> ResolveDependencyReferenceAsync(PackageItem parentPackage, PackageVersionGetResponse dependency, IAutomationInterface automationInterface, CancellationToken cancellationToken)
        {
            foreach (var packageServer in this.Where(x => x.Connected))
            {
                try
                {
                    var requestedDependency = new ConfigPlcPackage
                    {
                        Name = dependency.Name,
                        DistributorName = dependency.DistributorName,
                        Version = dependency.Version,
                        Branch = dependency.Branch,
                        Configuration = dependency.Configuration,
                        Target = dependency.Target
                    };

                    var resolvedDependency = await FetchPackageAsync(
                        parentPackage.ProjectName,
                        parentPackage.PlcName,
                        requestedDependency,
                        includeMetadata: true,
                        automationInterface: automationInterface,
                        cancellationToken: cancellationToken);

                    if (resolvedDependency?.PackageVersion?.Name != null)
                    {
                        var immediateDependencies = (resolvedDependency.PackageVersion?.Dependencies ?? new List<PackageVersionGetResponse>())
                            .Select(x =>
                            {
                                var match = resolvedDependency.Dependencies?.FirstOrDefault(y => BuildDependencyKey(y.PackageVersion) == BuildDependencyKey(x));
                                return new PackageItem
                                {
                                    PackageServer = match?.PackageServer ?? resolvedDependency.PackageServer,
                                    Config = match?.Config ?? new ConfigPlcPackage(x),
                                    PackageVersion = x
                                };
                            })
                            .ToList();

                        return new PackageItem()
                        {
                            PackageServer = packageServer,
                            Config = requestedDependency,
                            PackageVersion = resolvedDependency.PackageVersion,
                            Dependencies = immediateDependencies
                        };
                    }
                }
                catch
                { }
            }

            return null;
        }

        private static string BuildDependencyKey(string distributorName, string name, string version, string branch, string configuration, string target)
        {
            return $"{distributorName ?? string.Empty}|{name ?? string.Empty}|{version ?? string.Empty}|{branch ?? string.Empty}|{configuration ?? string.Empty}|{target ?? string.Empty}";
        }

        private static string BuildDependencyKey(ConfigPlcPackage dependency)
        {
            return BuildDependencyKey(dependency?.DistributorName, dependency?.Name, dependency?.Version, dependency?.Branch, dependency?.Configuration, dependency?.Target);
        }

        private static string BuildDependencyKey(PackageVersionGetResponse dependency)
        {
            return BuildDependencyKey(dependency?.DistributorName, dependency?.Name, dependency?.Version, dependency?.Branch, dependency?.Configuration, dependency?.Target);
        }

        public async Task<bool> DownloadPackageVersionAsync(PackageItem package, string downloadPath=null, CancellationToken cancellationToken = default)
        {
            var success = false;
            foreach (var packageServer in package.PackageServer != null ? new List<IPackageServer> { package.PackageServer } : this.Where(x => x.Connected))
            {
                try
                {
                    await packageServer.DownloadPackageVersionAsync(package.PackageVersion, checksumMode: ChecksumMode.IgnoreMismatch, downloadPath: downloadPath, cancellationToken: cancellationToken);
                    success = true;
                    break;
                }
                catch
                { }
            }

            if (!success)
            {
                _logger.Warn("[download] {0} {1} (distributor: {2}) not found on any server", package.PackageVersion.Title, package.PackageVersion.Version, package.PackageVersion.DistributorName);
            }

            return success;
        }
    }
}
