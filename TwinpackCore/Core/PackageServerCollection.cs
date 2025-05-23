﻿using NLog;
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

        public async Task<PackageItem> FetchPackageAsync(ConfigPlcPackage item, bool includeMetadata = false, IAutomationInterface automationInterface = null, CancellationToken cancellationToken = default)
        {
            return await FetchPackageAsync(null, null, null, item, includeMetadata, automationInterface, cancellationToken);
        }

        public async Task<PackageItem> FetchPackageAsync(string projectName, string plcName, ConfigPlcPackage item, bool includeMetadata = false, IAutomationInterface automationInterface = null, CancellationToken cancellationToken = default)
        {
            return await FetchPackageAsync(null, projectName, plcName, item, includeMetadata, automationInterface, cancellationToken);
        }

        public async Task<PackageItem> FetchPackageAsync(IPackageServer packageServer, string projectName, string plcName, ConfigPlcPackage item, bool includeMetadata = false, IAutomationInterface automationInterface=null, CancellationToken cancellationToken = default)
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
                var packageVersion = await ps.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name, Version = item.Version },
                                                                                  item.Branch, item.Configuration, item.Target,
                                                                                  cancellationToken: cancellationToken);

                if (packageVersion?.Name != null && item.Version == null && projectName != null && plcName != null)
                {
                    if (automationInterface != null)
                    {
                        var effectiveVersion = await automationInterface.ResolveEffectiveVersionAsync(projectName, plcName, packageVersion.Title);
                        var effectivePackageVersion = await ps.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name, Version = effectiveVersion },
                                                                                          item.Branch, item.Configuration, item.Target,
                                                                                          cancellationToken: cancellationToken);

                        if (effectivePackageVersion?.Name != null)
                            packageVersion = effectivePackageVersion;
                        else
                            _logger.Warn($"Package {packageVersion?.Name} {effectiveVersion}* not available!");
                    }
                    else
                    {
                        _logger.Warn($"Cannot resolve wildcard reference '{packageVersion.Name}' without automation interface");
                    }
                }

                var packageVersionLatest = await ps.GetPackageVersionAsync(new PlcLibrary { DistributorName = item.DistributorName, Name = item.Name },
                                                                                  item.Branch, item.Configuration, item.Target,
                                                                                  cancellationToken: cancellationToken);

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
                        catalogItem.PackageVersion.Dependencies = (await ResolvePackageDependenciesAsync(catalogItem, automationInterface, cancellationToken)).Select(x => x.PackageVersion).ToList();
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

        public async Task PullAsync(Config config, bool skipInternalPackages = false, IEnumerable<ConfigPlcPackage> filter = null, string cachePath = null, CancellationToken cancellationToken = default)
        {
            _logger.Info($"Pulling from Package Server(s) (skip internal packages: {(skipInternalPackages ? "true" : "false")})");
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
                            _logger.Warn($"{package.Name} {package.Version}: {ex.Message}");
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

        public async Task<List<PackageItem>> ResolvePackageDependenciesAsync(PackageItem package, IAutomationInterface automationInterface, CancellationToken cancellationToken = default)
        {
            var resolvedDependencies = new List<PackageItem>();
            var resolvedPackageServers = new List<IPackageServer>();
            foreach (var dependency in package?.PackageVersion?.Dependencies ?? new List<PackageVersionGetResponse>())
            {
                var version = dependency.Version;
                foreach (var packageServer in this.Where(x => x.Connected))
                {
                    try
                    {
                        var resolvedDependency = await FetchPackageAsync(
                            package.ProjectName,
                            package.PlcName,
                            new ConfigPlcPackage
                            {
                                Name = dependency.Name,
                                DistributorName = dependency.DistributorName,
                                Version = dependency.Version,
                                Branch = dependency.Branch,
                                Configuration = dependency.Configuration,
                                Target = dependency.Target
                            },
                            includeMetadata: true,
                            automationInterface: automationInterface,
                            cancellationToken: cancellationToken);

                        if (resolvedDependency?.PackageVersion?.Name != null)
                        {
                            resolvedDependencies.Add(new PackageItem() { PackageServer = packageServer, PackageVersion = resolvedDependency.PackageVersion });
                            break;
                        }
                    }
                    catch
                    { }
                }
            }

            return resolvedDependencies;
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
                _logger.Warn($"Package {package.PackageVersion.Title} {package.PackageVersion.Version} (distributor: {package.PackageVersion.DistributorName}) not found on any package server!");
            }

            return success;
        }
    }
}
