using NLog;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Exceptions;
using Twinpack.Models;
using Twinpack.Protocol;
using Twinpack.Protocol.Api;

namespace Twinpack.Core
{
    public class MirrorResult
    {
        public int Copied { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<string> Failures { get; } = new List<string>();
    }

    /// <summary>
    /// Copies usable packages from one package server to another.
    /// The library file Twinpack already installs is the intermediate form. A NuGet or Beckhoff
    /// target receives that library packed as a .nupkg, including repository files (.tmx, .tml, .tme)
    /// when the source package has them. A package that is only those repository files is published
    /// to a NuGet or Beckhoff target and skipped for a Twinpack target. A Twinpack target receives
    /// a PLC library through the package-version API.
    /// </summary>
    public static class PackageMirror
    {
        static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        const int PageSize = 50;

        /// <summary>
        /// Quiet time before each request to a remote target. Version checks and the push
        /// otherwise run back to back and the feed answers 429.
        /// </summary>
        internal static TimeSpan TargetRequestDelay = TimeSpan.FromSeconds(3);

        public static IPackageServer Resolve(string name)
        {
            return Resolve(name, PackagingServerRegistry.Servers);
        }

        public static IPackageServer Resolve(string name, IEnumerable<IPackageServer> servers)
        {
            var configured = (servers ?? Enumerable.Empty<IPackageServer>()).Where(x => x != null).ToList();
            var matches = configured
                .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 1)
                return matches[0];

            var known = configured
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            var available = known.Count == 0 ? "none" : string.Join(", ", known);

            if (matches.Count == 0)
                throw new ProtocolException($"Package server '{name}' is not configured. Configured servers: {available}");

            throw new ProtocolException($"Package server name '{name}' matches {matches.Count} configured servers. Configured servers: {available}");
        }

        public static async Task<MirrorResult> RunAsync(IPackageServer source, IPackageServer target, IEnumerable<string> packages = null, CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (ReferenceEquals(source, target) || SameServer(source, target))
                throw new ProtocolException("Source and target must be different package servers.");

            EnsureSupported(source, "source");
            EnsureSupported(target, "target");

            if (TargetRequestDelay > TimeSpan.Zero && IsRemote(target))
                _logger.Info("[mirror] pausing {0}s between requests to {1}", TargetRequestDelay.TotalSeconds, target.Name);

            var requested = (packages ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new MirrorResult();
            var versions = await ListVersionsAsync(source, requested, result, cancellationToken);
            _logger.Info("[mirror] {0} package version(s) on {1}", versions.Count, source.Name);

            foreach (var version in versions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await CopyVersionAsync(source, target, version, result, cancellationToken);
            }

            return result;
        }

        static async Task<List<PackageVersionGetResponse>> ListVersionsAsync(IPackageServer source, IReadOnlyList<string> requested, MirrorResult result, CancellationToken cancellationToken)
        {
            var nuget = source as NugetServer;
            if (nuget != null)
                return await ListNugetVersionsAsync(nuget, requested, result, cancellationToken);

            var versions = await ListTwinpackVersionsAsync((TwinpackServer)source, cancellationToken);
            if (requested.Count == 0)
                return versions;

            var selected = versions
                .Where(version => requested.Any(name => string.Equals(version.Name, name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var name in requested)
            {
                if (selected.Any(version => string.Equals(version.Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                result.Failed++;
                result.Failures.Add("Package '" + name + "' was not found on " + source.Name);
            }

            return selected;
        }

        static async Task<List<PackageVersionGetResponse>> ListNugetVersionsAsync(NugetServer source, IReadOnlyList<string> requested, MirrorResult result, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> ids;
            if (requested.Count == 0)
            {
                ids = await source.ListPackageIdsAsync(cancellationToken);
            }
            else
            {
                var found = new List<string>();
                foreach (var name in requested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var matches = await source.FindPackageIdsAsync(name, cancellationToken);
                    if (matches.Count == 0)
                    {
                        result.Failed++;
                        result.Failures.Add("Package '" + name + "' was not found on " + source.Name);
                        continue;
                    }

                    foreach (var id in matches)
                    {
                        if (found.Any(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        found.Add(id);
                    }
                }

                ids = found;
            }

            var versions = new List<PackageVersionGetResponse>();
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<NuGetVersion> packageVersions;
                try
                {
                    packageVersions = await source.ListPackageVersionsAsync(id, cancellationToken);
                }
                catch (Exception ex) when (!CancelableTask.IsCancellation(ex))
                {
                    _logger.Warn("[mirror] failed to list versions of {0}: {1}", id, ex.Message);
                    _logger.Trace(ex);
                    continue;
                }

                foreach (var version in packageVersions)
                {
                    versions.Add(new PackageVersionGetResponse
                    {
                        Name = id,
                        Version = version.OriginalVersion ?? version.ToNormalizedString(),
                        Branch = "main",
                        Target = "TC3.1",
                        Configuration = "Release"
                    });
                }
            }

            return versions;
        }

        static async Task<List<PackageVersionGetResponse>> ListTwinpackVersionsAsync(TwinpackServer source, CancellationToken cancellationToken)
        {
            var versions = new List<PackageVersionGetResponse>();
            var page = 1;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var catalog = await source.GetCatalogAsync("", page, PageSize, cancellationToken);
                var items = catalog?.Item1?.Where(x => !string.IsNullOrWhiteSpace(x?.Name)).ToList() ?? new List<CatalogItemGetResponse>();
                if (items.Count == 0)
                    break;

                foreach (var item in items)
                    versions.AddRange(await ListTwinpackPackageVersionsAsync(source, item, cancellationToken));

                if (catalog.Item2 != true)
                    break;

                page++;
            }

            return versions;
        }

        static async Task<List<PackageVersionGetResponse>> ListTwinpackPackageVersionsAsync(TwinpackServer source, CatalogItemGetResponse item, CancellationToken cancellationToken)
        {
            var versions = new List<PackageVersionGetResponse>();
            var library = new PlcLibrary { Name = item.Name, DistributorName = item.DistributorName };
            var page = 1;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var listed = await source.GetPackageVersionsAsync(library, page: page, perPage: PageSize, cancellationToken: cancellationToken);
                var batch = listed?.Item1?.ToList() ?? new List<PackageVersionGetResponse>();
                if (batch.Count == 0)
                    break;

                foreach (var version in batch)
                {
                    if (string.IsNullOrWhiteSpace(version.Name))
                        version.Name = item.Name;
                    if (string.IsNullOrWhiteSpace(version.DistributorName))
                        version.DistributorName = item.DistributorName;
                    versions.Add(version);
                }

                if (listed.Item2 != true)
                    break;

                page++;
            }

            return versions;
        }

        static async Task CopyVersionAsync(IPackageServer source, IPackageServer target, PackageVersionGetResponse summary, MirrorResult result, CancellationToken cancellationToken)
        {
            var name = summary?.Name ?? "";
            var version = summary?.Version ?? "";
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
                return;

            var tempRoot = Path.Combine(Path.GetTempPath(), "twinpack-mirror-" + Guid.NewGuid().ToString("N"));
            try
            {
                PackageVersionGetResponse package = null;
                try
                {
                    package = await source.GetPackageVersionAsync(
                        new PlcLibrary { Name = name, Version = version, DistributorName = summary.DistributorName },
                        summary.Branch,
                        summary.Configuration,
                        summary.Target,
                        cancellationToken);
                }
                catch (Exception ex) when (IsNoPlcLibrary(ex))
                {
                    if (!(target is NugetServer))
                    {
                        Skip(result, summary, target, "no PLC library (repository files such as .tmx are not mirrored to a Twinpack server)");
                        return;
                    }
                }

                if (string.IsNullOrEmpty(package?.Name))
                    package = summary;
                if (string.IsNullOrEmpty(package.Version))
                    package.Version = version;
                if (string.IsNullOrEmpty(package.DistributorName))
                    package.DistributorName = summary.DistributorName;

                // Download still needs the feed id. A PLC library is published under its library name.
                // A TMX package has no PLC library, so it keeps the feed id.
                var feedName = package.Name;
                var feedVersion = package.Version;
                if (IsTmxPackage(feedName))
                {
                    if (!(target is NugetServer))
                    {
                        Skip(result, package, target, "repository files (.tmx, .tml, .tme) are not uploaded to a Twinpack server");
                        return;
                    }

                    if (await TargetHasVersionAsync(target, package, cancellationToken))
                    {
                        Skip(result, package, target, "already on " + target.Name);
                        return;
                    }
                }
                else
                {
                    package.Name = LibraryName(source, package);
                    if (await TargetHasVersionAsync(target, package, cancellationToken))
                    {
                        Skip(result, package, target, "already on " + target.Name);
                        return;
                    }

                    package.Name = feedName;
                    package.Version = feedVersion;
                }

                Directory.CreateDirectory(tempRoot);
                await source.DownloadPackageVersionAsync(package, ChecksumMode.IgnoreMismatch, tempRoot, cancellationToken);
                var libraryFileBeforeRename = FindLibrary(package, tempRoot);
                var libraryFile = libraryFileBeforeRename;
                if (libraryFile != null)
                    package.Name = LibraryName(source, package);
                libraryFile = libraryFile ?? FindLibrary(package, tempRoot);

                if (libraryFile == null)
                {
                    if (target is NugetServer && package.RepositoryFiles != null && package.RepositoryFiles.Count > 0)
                    {
                        package.Name = feedName;
                        package.Version = string.IsNullOrEmpty(package.Version) ? feedVersion : package.Version;
                        if (await TargetHasVersionAsync(target, package, cancellationToken))
                        {
                            Skip(result, package, target, "already on " + target.Name);
                            return;
                        }

                        await PushNugetAsync((NugetServer)target, package, null, tempRoot, cancellationToken);
                        result.Copied++;
                        _logger.Info("[mirror] copied {0} {1} to {2}", package.Name, package.Version, target.Name);
                        return;
                    }

                    Skip(result, package, target, "no PLC library (repository files such as .tmx are not mirrored to a Twinpack server)");
                    return;
                }

                if (source is BeckhoffServer)
                    UseLibraryNames(package.Dependencies);

                if (await TargetHasVersionAsync(target, package, cancellationToken))
                {
                    Skip(result, package, target, "already on " + target.Name);
                    return;
                }

                var nugetTarget = target as NugetServer;
                if (nugetTarget != null)
                    await PushNugetAsync(nugetTarget, package, libraryFile, tempRoot, cancellationToken);
                else
                    await PushTwinpackAsync((TwinpackServer)target, package, libraryFile, cancellationToken);

                result.Copied++;
                _logger.Info("[mirror] copied {0} {1} to {2}", package.Name, package.Version, target.Name);
            }
            catch (Exception ex) when (IsNoPlcLibrary(ex))
            {
                result.Skipped++;
                _logger.Info("[mirror] skip {0} {1}, no PLC library to mirror ({2})", name, version, ex.Message);
            }
            catch (Exception ex) when (IsAlreadyPresent(ex))
            {
                result.Skipped++;
                _logger.Info("[mirror] skip {0} {1}, already on {2}", name, version, target.Name);
            }
            catch (Exception ex) when (!CancelableTask.IsCancellation(ex))
            {
                Fail(result, name, version, ex);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        /// <summary>
        /// A Beckhoff feed id such as <c>TwinCAT.XAE.PLC.Lib.Tc2_Standard</c> is the library
        /// <c>Tc2_Standard</c>. The mirrored NuGet package uses that library name, which is what
        /// Twinpack looks up on a NuGet server.
        /// </summary>
        static string LibraryName(IPackageServer source, PackageVersionGetResponse package)
        {
            if (!(source is BeckhoffServer))
                return package.Name;

            if (!string.IsNullOrWhiteSpace(package.Title))
                return package.Title;

            var last = package.Name?.Split('.').LastOrDefault();
            return string.IsNullOrWhiteSpace(last) ? package.Name : last;
        }

        /// <summary>
        /// Dependencies are published under the same library name as the package itself, so a
        /// mirrored Beckhoff feed resolves <c>Tc2_Standard</c> instead of the feed id.
        /// </summary>
        static void UseLibraryNames(IEnumerable<PackageVersionGetResponse> dependencies)
        {
            if (dependencies == null)
                return;

            foreach (var dependency in dependencies)
            {
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.Name))
                    continue;

                if (!string.IsNullOrWhiteSpace(dependency.Title))
                {
                    dependency.Name = dependency.Title;
                    continue;
                }

                var last = dependency.Name.Split('.').LastOrDefault();
                if (!string.IsNullOrWhiteSpace(last))
                    dependency.Name = last;
            }
        }

        static async Task<bool> TargetHasVersionAsync(IPackageServer target, PackageVersionGetResponse package, CancellationToken cancellationToken)
        {
            await PauseBeforeTargetRequestAsync(target, "version check", package?.Name, package?.Version, cancellationToken);

            var nuget = target as NugetServer;
            if (nuget != null)
            {
                var published = AutomationInterface.NugetVersion(package.Version);
                if (!NuGetVersion.TryParse(published, out var wanted))
                    return false;

                var present = await nuget.ListPackageVersionsAsync(package.Name, cancellationToken);
                return present.Any(version => VersionComparer.Default.Equals(version, wanted));
            }

            var existing = await target.GetPackageVersionAsync(
                new PlcLibrary { Name = package.Name, Version = package.Version, DistributorName = package.DistributorName },
                package.Branch,
                package.Configuration,
                package.Target,
                cancellationToken);
            return SameIdentity(existing, package);
        }

        static async Task PushNugetAsync(NugetServer target, PackageVersionGetResponse package, string libraryFile, string tempRoot, CancellationToken cancellationToken)
        {
            await PauseBeforeTargetRequestAsync(target, "push", package?.Name, package?.Version, cancellationToken);

            var output = Path.Combine(tempRoot, "nupkg");
            // The nuspec title is the name TwinCAT's library manager registers. A Beckhoff feed id
            // in DisplayName must not replace the library title (Tc2_XmlDataSrv, not the feed id).
            var libraryTitle = string.IsNullOrWhiteSpace(package.Title) ? package.Name : package.Title;
            var plc = new ConfigPlcProject
            {
                Name = package.Name,
                Version = package.Version,
                Title = libraryTitle,
                DistributorName = package.DistributorName ?? "",
                Packages = (package.Dependencies ?? new List<PackageVersionGetResponse>())
                    .Where(dependency => !string.IsNullOrWhiteSpace(dependency?.Name))
                    .Select(dependency => new ConfigPlcPackage(dependency))
                    .ToList()
            };
            var metadata = new PlcPublishMetadata
            {
                Description = package.Description,
                Authors = package.Authors,
                DisplayName = libraryTitle,
                ProjectUrl = package.ProjectUrl,
                Entitlement = package.Entitlement,
                License = package.License
            };

            var repositoryFiles = RepositoryPackFiles(package);
            string nupkg;
            if (string.IsNullOrEmpty(libraryFile))
            {
                nupkg = NugetPackService.PackRepository(plc, repositoryFiles, output, metadata);
            }
            else
            {
                nupkg = NugetPackService.PackLibrary(
                    plc,
                    libraryFile,
                    string.IsNullOrWhiteSpace(package.Target) ? "TC3.1" : package.Target,
                    package.Compiled == 1,
                    output,
                    metadata,
                    repositoryFiles);
            }

            await target.PushNupkgAsync(nupkg, cancellationToken);
        }

        static List<NugetPackFile> RepositoryPackFiles(PackageVersionGetResponse package)
        {
            var files = new List<NugetPackFile>();
            if (package?.RepositoryFiles == null)
                return files;

            const string marker = ".repository" + "\\";
            foreach (var file in package.RepositoryFiles)
            {
                if (string.IsNullOrEmpty(file) || !File.Exists(file))
                    continue;

                var normalized = file.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    continue;

                var relative = normalized.Substring(index + marker.Length);
                var directory = Path.GetDirectoryName(relative) ?? "";
                var target = string.IsNullOrEmpty(directory)
                    ? "repository"
                    : "repository/" + directory.Replace(Path.DirectorySeparatorChar, '/');
                files.Add(new NugetPackFile { Source = file, Target = target });
            }

            return files;
        }

        static bool IsTmxPackage(string packageId)
        {
            return packageId != null && packageId.IndexOf(".TMX.", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static async Task PushTwinpackAsync(TwinpackServer target, PackageVersionGetResponse package, string libraryFile, CancellationToken cancellationToken)
        {
            await PauseBeforeTargetRequestAsync(target, "push", package?.Name, package?.Version, cancellationToken);

            var request = new PackageVersionPostRequest
            {
                Name = package.Name,
                Title = package.Title,
                Version = package.Version,
                Target = package.Target,
                Compiled = package.Compiled,
                License = package.License,
                Description = package.Description,
                Authors = package.Authors,
                DisplayName = package.DisplayName,
                ProjectUrl = package.ProjectUrl,
                Entitlement = package.Entitlement,
                Branch = package.Branch,
                Configuration = package.Configuration,
                Notes = package.Notes,
                DistributorName = package.DistributorName,
                BinaryDownloadUrl = package.BinaryDownloadUrl,
                LicenseTmcBinary = package.LicenseTmcBinary,
                LicenseBinary = package.LicenseBinary,
                Binary = Convert.ToBase64String(File.ReadAllBytes(libraryFile)),
                Dependencies = package.Dependencies?.Select(dependency => new PackageVersionDependency
                {
                    Repository = dependency.Repository,
                    DistributorName = dependency.DistributorName,
                    Name = dependency.Name,
                    Version = dependency.Version,
                    Branch = dependency.Branch,
                    Target = dependency.Target,
                    Configuration = dependency.Configuration
                }).ToList()
            };

            await target.PostPackageVersionAsync(request, cancellationToken);
        }

        static string FindLibrary(PackageVersionGetResponse package, string cacheRoot)
        {
            var saved = package.LibraryFiles?
                .Where(file => !string.IsNullOrEmpty(file) && File.Exists(file) && PlcLibraryPackage.IsLibraryFile(file))
                .OrderBy(file => Path.GetFileName(file).Length)
                .FirstOrDefault();
            if (saved != null)
                return saved;

            var expected = LibraryFile(cacheRoot, package);
            if (File.Exists(expected))
                return expected;

            if (!Directory.Exists(cacheRoot))
                return null;

            return Directory.GetFiles(cacheRoot, "*", SearchOption.AllDirectories)
                .Where(file => PlcLibraryPackage.IsLibraryFile(file))
                .Where(file => file.IndexOf(".repository", StringComparison.OrdinalIgnoreCase) < 0)
                .OrderBy(file => Path.GetFileName(file).Length)
                .FirstOrDefault();
        }

        static string LibraryFile(string cacheRoot, PackageVersionGetResponse packageVersion)
        {
            var extension = packageVersion.Compiled == 1 ? "compiled-library" : "library";
            var directory = string.IsNullOrEmpty(packageVersion.Target)
                ? cacheRoot
                : Path.Combine(cacheRoot, packageVersion.Target);
            return Path.Combine(directory, $"{packageVersion.Name}_{packageVersion.Version}.{extension}");
        }

        static bool SameIdentity(PackageVersionGetResponse existing, PackageVersionGetResponse wanted)
        {
            if (existing == null || string.IsNullOrEmpty(existing.Name) || string.IsNullOrEmpty(existing.Version))
                return false;

            return string.Equals(existing.Name, wanted.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Version, wanted.Version, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Branch ?? "", wanted.Branch ?? "", StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Target ?? "", wanted.Target ?? "", StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Configuration ?? "", wanted.Configuration ?? "", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsNoPlcLibrary(Exception ex)
        {
            if (ex is LibraryFileInvalidException)
                return true;

            var protocol = ex as ProtocolException;
            if (protocol == null)
                return false;

            var message = protocol.Message ?? "";
            return message.IndexOf("nupkg", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf(".tmx", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("PLC library", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void Skip(MirrorResult result, PackageVersionGetResponse package, IPackageServer target, string reason)
        {
            result.Skipped++;
            _logger.Info("[mirror] skip {0} {1}, {2}", package.Name, package.Version, reason);
        }

        static void EnsureSupported(IPackageServer server, string role)
        {
            if (server is NugetServer || server is TwinpackServer)
                return;

            throw new ProtocolException($"Cannot use {role} '{server.Name}' ({server.ServerType}). Mirror supports NuGet, Beckhoff, and Twinpack servers.");
        }

        static bool SameServer(IPackageServer source, IPackageServer target)
        {
            if (string.Equals(source.Name, target.Name, StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(Normalize(source.UrlBase), Normalize(target.UrlBase), StringComparison.OrdinalIgnoreCase);
        }

        static string Normalize(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? "" : url.Trim().TrimEnd('/', '\\');
        }

        static bool IsRemote(IPackageServer server)
        {
            return Uri.TryCreate(server?.UrlBase, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        static async Task PauseBeforeTargetRequestAsync(IPackageServer target, string action, string packageName, string packageVersion, CancellationToken cancellationToken)
        {
            if (TargetRequestDelay <= TimeSpan.Zero || !IsRemote(target))
                return;

            _logger.Trace("[mirror] waiting {0}s before {1} of {2} {3} on {4}",
                TargetRequestDelay.TotalSeconds, action, packageName, packageVersion, target.Name);
            await Task.Delay(TargetRequestDelay, cancellationToken);
        }

        static bool IsAlreadyPresent(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var message = current.Message ?? "";
                if (message.IndexOf("409", StringComparison.Ordinal) >= 0
                    || message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static void Fail(MirrorResult result, string id, string version, Exception ex)
        {
            result.Failed++;
            var label = string.IsNullOrEmpty(version) ? id : id + " " + version;
            result.Failures.Add(label + ": " + ex.Message);
            _logger.Warn("[mirror] failed {0}: {1}", label, ex.Message);
            _logger.Trace(ex);
        }
    }
}
