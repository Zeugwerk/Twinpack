using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace Twinpack.Application
{
    /// <summary>
    /// Resolves package metadata and collects the transitive dependency closure, preserving order:
    /// each batch is fully resolved and appended before dependency batches are processed depth-first.
    /// </summary>
    internal static class AffectedPackagesClosureBuilder
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static Task<List<PackageItem>> BuildAsync(
            IAutomationInterface automationInterface,
            IPackageServerCollection packageServers,
            List<PackageItem> packages,
            List<PackageItem> cache,
            bool includeDependencies,
            CancellationToken cancellationToken)
        {
            var seenKeys = new HashSet<(string Project, string Plc, string Name)>();
            foreach (var existing in cache)
            {
                if (existing.PackageVersion?.Name != null)
                    seenKeys.Add(MakeKey(existing));
            }

            return ProcessBatchAsync(
                automationInterface,
                packageServers,
                packages,
                cache,
                seenKeys,
                includeDependencies,
                cancellationToken);
        }

        private static (string Project, string Plc, string Name) MakeKey(PackageItem p) =>
            (p.ProjectName ?? string.Empty, p.PlcName ?? string.Empty, p.PackageVersion?.Name ?? string.Empty);

        private static async Task<List<PackageItem>> ProcessBatchAsync(
            IAutomationInterface automationInterface,
            IPackageServerCollection packageServers,
            IReadOnlyList<PackageItem> packages,
            List<PackageItem> cache,
            HashSet<(string Project, string Plc, string Name)> seenKeys,
            bool includeDependencies,
            CancellationToken cancellationToken)
        {
            foreach (var package in packages)
            {
                await EnsureResolvedAsync(automationInterface, packageServers, package, cancellationToken).ConfigureAwait(false);

                if (package.PackageVersion?.Name == null)
                {
                    Logger.Trace($"Package {package.PlcPackageReference?.Name} {package.PlcPackageReference?.Version ?? "*"} could not be found!");
                    continue;
                }

                if (seenKeys.Add(MakeKey(package)))
                    cache.Add(package);
            }

            if (!includeDependencies)
                return cache;

            foreach (var package in packages)
            {
                var dependencies = package.PackageVersion?.Dependencies;
                if (dependencies == null || dependencies.Count == 0)
                    continue;

                var dependencyItems = CreateDependencyPackageItems(package, packages);
                await ProcessBatchAsync(
                    automationInterface,
                    packageServers,
                    dependencyItems,
                    cache,
                    seenKeys,
                    includeDependencies: true,
                    cancellationToken).ConfigureAwait(false);
            }

            return cache;
        }

        private static async Task EnsureResolvedAsync(
            IAutomationInterface automationInterface,
            IPackageServerCollection packageServers,
            PackageItem package,
            CancellationToken cancellationToken)
        {
            if (package.Package != null && package.PackageVersion != null && package.PackageServer != null)
                return;

            package.Package = null;
            package.PackageVersion = null;
            package.PackageServer = null;

            var resolvedPackage = await packageServers.FetchPackageAsync(
                package.PackageServer,
                package.ProjectName,
                package.PlcName,
                package.PlcPackageReference,
                includeMetadata: true,
                automationInterface,
                cancellationToken).ConfigureAwait(false);

            package.Package ??= resolvedPackage.Package;
            package.PackageVersion ??= resolvedPackage.PackageVersion;
            package.PackageServer ??= resolvedPackage.PackageServer;
        }

        private static List<PackageItem> CreateDependencyPackageItems(PackageItem parent, IReadOnlyList<PackageItem> originatingBatch)
        {
            var list = new List<PackageItem>();
            foreach (var dependency in parent.PackageVersion.Dependencies)
            {
                var hintServer = originatingBatch
                    .FirstOrDefault(y => (y.PlcPackageReference?.Name ?? y.PackageVersion?.Name) == dependency.Name)
                    ?.PackageServer;

                list.Add(new PackageItem
                {
                    PackageServer = hintServer,
                    ProjectName = parent.ProjectName,
                    PlcName = parent.PlcName,
                    Catalog = new CatalogPackageSummary { Name = dependency.Name },
                    Package = dependency,
                    PackageVersion = dependency,
                    PlcPackageReference = new PlcPackageReference(dependency) { Options = parent.PlcPackageReference?.Options?.CopyForDependency() }
                });
            }

            return list;
        }
    }
}
