using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using System.Reflection;
using AdysTech.CredentialManager;
using System.Threading;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Versioning;
using WixToolset.Dtf.WindowsInstaller;
using WixToolset.Dtf.WindowsInstaller.Package;
using NuGet.Packaging.Core;
using Twinpack.Exceptions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Twinpack.Protocol
{
    class NugetPackagingServerFactory : IPackagingServerFactory
    {
        public IPackageServer Create(string name, string uri)
        {
            return new NugetServer(name, uri);
        }

        public string ServerType { get; } = "NuGet Repository";
    }

    public class NugetServer : IPackageServer
    {
        SourceRepository _sourceRepository;
        SourceCacheContext _cache;
        SourceCacheContext _noCache;

        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public static string DefaultLibraryCachePath { get { return Path.Combine(Directory.GetCurrentDirectory(), ".Zeugwerk", "libraries"); } }
        public string ServerType { get; } = "NuGet Repository";
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;
        public string UrlBase { get; set; }
        public string Url
        {
            get => UrlBase;
        }
        public virtual string UrlRegister
        {
            get => null;
        }

        public string Username { get; set; }
        public string Password { get; set; }
        public LoginPostResponse UserInfo { get; set; }
        public bool LoggedIn { get { return Connected && UserInfo?.Configurations?.FirstOrDefault()?.IsPrivate == true; } }
        public bool Connected { get { return UserInfo?.User != null; } }
        protected virtual string SearchPrefix { get => "";}
        protected virtual bool ResolveForeignDependencies { get => true; }
        protected virtual string IconUrl { get => null; }

        public NugetServer(string name = "", string url = null)
        {
            Name = name;
            UrlBase = url;
            InvalidateCache();
            Username = null;
            Password = null;
        }

        public Version ClientVersion
        {
            get
            {
                try
                {
                    return Assembly.GetExecutingAssembly()?.GetName()?.Version ?? Assembly.GetEntryAssembly()?.GetName()?.Version;
                }
                catch (Exception)
                {
                    return new Version("0.0.0.0");
                }
            }
        }

        public bool IsClientUpdateAvailable
        {
            get
            {
                return UserInfo?.UpdateVersion != null && new Version(UserInfo?.UpdateVersion) > ClientVersion;
            }
        }

#pragma warning disable CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        public async Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        {
            throw new NotImplementedException();
        }

        protected async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchPrefix, string search, int skip, int take, CancellationToken cancellationToken)
        {
            ILogger logger = NullLogger.Instance;
            PackageSearchResource resource = await _sourceRepository.GetResourceAsync<PackageSearchResource>(cancellationToken);

            return await resource.SearchAsync(
                searchPrefix + (search ?? "").Replace(" ", "_"),
                new SearchFilter(includePrerelease: true),
                skip: skip,
                take: take,
                logger,
                cancellationToken);
        }

        public virtual async Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            if(_sourceRepository == null)
                return new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(new List<CatalogItemGetResponse>(), false);

            try
            {
                var results = await SearchAsync(SearchPrefix, search, perPage * (page - 1), perPage, cancellationToken);
                var packages = await Task.WhenAll(results
                        .Where(x => x.Tags.ToLower().Contains("library") || x.Tags.ToLower().Contains("plc-library"))
                        .Select(async x =>
                            new CatalogItemGetResponse()
                            {
                                PackageId = null,
                                Name = x.Identity.Id,
                                DistributorName = x.Authors,
                                Description = x.Description,
                                IconUrl = x.IconUrl?.ToString() ?? IconUrl,
                                RuntimeLicense = 0,
                                DisplayName = x.Identity.Id,
                                Downloads = x.DownloadCount.HasValue && x.DownloadCount.Value > 0 ? ((int?)x.DownloadCount.Value) : null,
                                Created = x.Published?.ToString() ?? "Unknown",
                                Modified = x.Published?.ToString() ?? "Unknown"
#if !TWINPACK_HEADLESS
                                ,IconBytes = await GetPackageIconAsync(x.Identity, cancellationToken),
#endif
                            }));

                return new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(packages, results.Any());
            }
            catch(Exception)
            {
                throw;
            }

        }

#if !TWINPACK_HEADLESS
        // A missing icon is cached too, so a package with no <icon> is not downloaded again.
        private sealed class CachedIcon
        {
            public byte[] Bytes { get; set; }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CachedIcon> IconCache =
            new System.Collections.Concurrent.ConcurrentDictionary<string, CachedIcon>(StringComparer.OrdinalIgnoreCase);

        protected virtual async Task<byte[]> GetPackageIconAsync(PackageIdentity identity, CancellationToken cancellationToken)
        {
            if (identity?.Version == null)
                return null;

            var key = (UrlBase ?? "") + "\n" + identity.Id + "\n" + identity.Version.ToNormalizedString();
            if (IconCache.TryGetValue(key, out var cached))
                return cached.Bytes;

            var bytes = await ReadPackageIconAsync(identity, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
                IconCache[key] = new CachedIcon { Bytes = bytes };

            return bytes;
        }

        private async Task<byte[]> ReadPackageIconAsync(PackageIdentity identity, CancellationToken cancellationToken)
        {
            // Prefer the NuGet on-disk cache. A cached nupkg from before the package had an icon
            // must not win, so a package that opens but has no <icon> is tried against the feed too.
            var openedPackage = false;
            foreach (var cache in new[] { _cache, _noCache })
            {
                try
                {
                    FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();
                    using (MemoryStream packageStream = new MemoryStream())
                    {
                        await resource.CopyNupkgToStreamAsync(
                            identity.Id,
                            identity.Version,
                            packageStream,
                            cache,
                            NullLogger.Instance,
                            cancellationToken);

                        packageStream.Position = 0;
                        using (PackageArchiveReader packageReader = new PackageArchiveReader(packageStream))
                        {
                            var bytes = ReadIconBytes(packageReader);
                            if (bytes != null)
                                return bytes;

                            openedPackage = true;
                        }
                    }
                }
                catch (Exception ex) when (!CancelableTask.IsCancellation(ex))
                {
                    _logger.Trace(ex);
                }
            }

            if (!openedPackage && !cancellationToken.IsCancellationRequested)
                _logger.Error("Can't unpack package icon: {0} {1}", identity.Id, identity.Version);

            return null;
        }

        static byte[] ReadIconBytes(PackageArchiveReader packageReader)
        {
            var iconPath = packageReader.NuspecReader.GetIcon();
            if (string.IsNullOrWhiteSpace(iconPath))
                return null;

            var normalized = iconPath.Replace('\\', '/').TrimStart('/');
            var entry = packageReader.GetEntry(normalized) ?? packageReader.GetEntry(iconPath);
            if (entry == null)
            {
                var match = packageReader.GetFiles().FirstOrDefault(file =>
                    string.Equals(file.Replace('\\', '/').TrimStart('/'), normalized, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    entry = packageReader.GetEntry(match);
            }

            if (entry == null)
                return null;

            using (var entryStream = entry.Open())
            using (var memory = new MemoryStream())
            {
                entryStream.CopyTo(memory);
                return memory.ToArray();
            }
        }
#endif

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, string branch = null, string configuration = null, string target = null, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            if (_sourceRepository == null)
                return new Tuple<IEnumerable<PackageVersionGetResponse>, bool>(new List<PackageVersionGetResponse>(), false);

            PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();

            IEnumerable<IPackageSearchMetadata> results = await resource.GetMetadataAsync(
                library.Name,
                includePrerelease: true,
                includeUnlisted: false,
                _cache,
                NullLogger.Instance,
                cancellationToken);

            return
                new Tuple<IEnumerable<PackageVersionGetResponse>, bool>(
                    results
                    .OrderByDescending(x => x.Identity.Version)
                    .Select(x => new PackageVersionGetResponse()
                    {
                        PackageId = null,
                        Name = x.Identity.Id,
                        DistributorName = x.Authors,
                        DisplayName = x.Identity.Id,
                        Description = x.Description,
                        Entitlement = null,
                        ProjectUrl = x.ProjectUrl?.ToString(),
                        IconUrl = x.IconUrl?.ToString() ?? IconUrl,
                        Authors = x.Authors,
                        License = x.LicenseMetadata?.License,
                        LicenseTmcBinary = null,
                        LicenseBinary = null,
                        Branches = new List<string>() { "main" },
                        Targets = new List<string>() { "TC3.1" },
                        Configurations = new List<string>() { "Release" },
                        Version = EvaluateVersion(x.Identity.Version),
                        Branch = "main",
                        Target = "TC3.1",
                        Configuration = "Release",
                        Compiled = EvaluateCompiled(x.Tags),
                        Released = x.Identity.Version.IsPrerelease == true ? 1 : 0,
                        //Notes =,
                        //PackageType,
                        Binary = null,
                        BinaryDownloadUrl = null,
                        BinarySha256 = null,
                    }).ToList(), false);
        }

        public virtual async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default)
        {
            if (library.Name.Contains(" "))
            {
                var package = await SearchAsync("", library.Name, 0, 1, cancellationToken);
                library.Name = package.FirstOrDefault()?.Identity.Id ?? library.Name;
                
            }
            return await GetPackageVersionAsync(library, preferredBranch, preferredConfiguration, preferredTarget, cancellationToken);
        }

        public async Task DownloadPackageVersionAsync(PackageVersionGetResponse packageVersion, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default)
        {
            if (_sourceRepository == null)
                return;

            FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

            using (MemoryStream packageStream = new MemoryStream())
            {
                NuGetVersion version;
                if (packageVersion.Version == null)
                {
                    PackageMetadataResource meta = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
                    IEnumerable<IPackageSearchMetadata> metaData = await meta.GetMetadataAsync(
                        packageVersion.Name,
                        includePrerelease: true,
                        includeUnlisted: false,
                        _cache,
                        NullLogger.Instance,
                        cancellationToken);
                    version = metaData.OrderByDescending(p => p.Identity.Version).FirstOrDefault()?.Identity.Version;
                }
                else
                {
                    // the published version is never reconstructed from the four part one, because more
                    // than one published version can produce the same four part version: a package using
                    // the superseded convention that put the revision in the prerelease yields the same
                    // one as a package that spells all four parts out. Asking the feed which versions it
                    // has and matching against those is unambiguous.
                    var published = await resource.GetAllVersionsAsync(packageVersion.Name, _cache, NullLogger.Instance, cancellationToken);
                    version = published.FirstOrDefault(x => VersionMatches(x, packageVersion.Version))
                        ?? new NuGetVersion(packageVersion.Version);
                }

                await resource.CopyNupkgToStreamAsync(
                    packageVersion.Name,
                    version,
                    packageStream,
                    _noCache,
                    NullLogger.Instance,
                    cancellationToken);

                using (PackageArchiveReader packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageFiles = await packageReader.GetPackageFilesAsync(PackageSaveMode.Files, cancellationToken);

                    var msis = packageFiles.Where(x => x.EndsWith(".msi", StringComparison.InvariantCultureIgnoreCase)).ToList();
                    var packedLibraries = packageFiles.Where(PlcLibraryPackage.IsLibraryFile).ToList();
                    var packedRepositoryFiles = packageFiles.Where(IsRepositoryFile).ToList();

                    string tempFolderPath = null;
                    string tempFilePath = null;
                    try
                    {
                        if (packedLibraries.Count > 0 || packedRepositoryFiles.Count > 0)
                        {
                            tempFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(tempFolderPath);
                            foreach (var relativePath in packedLibraries)
                                await ExtractPackageFileAsync(packageReader, relativePath, Path.Combine(tempFolderPath, Path.GetFileName(relativePath)));
                            foreach (var relativePath in packedRepositoryFiles)
                            {
                                var destination = Path.Combine(tempFolderPath, relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
                                await ExtractPackageFileAsync(packageReader, relativePath, destination);
                            }
                        }
                        else if (msis.Count == 1)
                        {
                            var entry = packageReader.GetEntry(msis[0]);
                            if (entry == null)
                                throw new Exceptions.ProtocolException("nupkg does not contain the expected .msi");

                            tempFilePath = Path.GetTempFileName();
                            tempFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(tempFolderPath);
                            using (var output = File.Create(tempFilePath))
                            using (var input = entry.Open())
                                await input.CopyToAsync(output);

                            using (var pkg = new InstallPackage(tempFilePath, DatabaseOpenMode.ReadOnly))
                            {
                                pkg.WorkingDirectory = tempFolderPath;
                                pkg.ExtractFiles();
                            }
                        }
                        else
                        {
                            throw new Exceptions.ProtocolException("nupkg should contain PLC libraries, repository files (.tmx, .tml, .tme), or a single .msi");
                        }

                        var extracted = PlcLibraryPackage.DescribeAll(
                            Directory.GetFiles(tempFolderPath, "*", SearchOption.AllDirectories));
                        var cacheDirectory = Path.Combine(cachePath ?? DefaultLibraryCachePath, packageVersion.Target ?? "");
                        Directory.CreateDirectory(cacheDirectory);
                        var repositoryFiles = CopyRepositoryFiles(tempFolderPath, cacheDirectory, packageVersion);

                        if (extracted.Count == 0)
                        {
                            if (repositoryFiles.Count == 0)
                                throw new Exceptions.ProtocolException("nupkg does not contain a PLC library (.library or .compiled-library)");

                            packageVersion.LibraryFiles = new List<string>();
                            packageVersion.RepositoryFiles = repositoryFiles;
                            _logger.Info("[download] {0} ships {1} repository file(s) (.tml/.tmx/.tme)", packageVersion.Name, repositoryFiles.Count);
                        }
                        else
                        {
                            var reference = PlcLibraryPackage.ChooseReference(extracted, packageVersion.Name, packageVersion.Title);
                            if (reference == null)
                                throw new Exceptions.ProtocolException(
                                    $"nupkg contains {extracted.Count} libraries ({string.Join(", ", extracted.Select(x => x.Title))}) and none matches '{packageVersion.Name}'");

                            var saved = new List<string>();
                            foreach (var library in extracted)
                            {
                                var suffix = PlcLibraryPackage.LibrarySuffix(library.Path);
                                var fileName = library == reference
                                    ? $"{SafeFileName(packageVersion.Name)}_{packageVersion.Version}{suffix}"
                                    : $"{SafeFileName(packageVersion.Name)}_{packageVersion.Version}_{SafeFileName(library.Title)}{suffix}";
                                var destination = Path.Combine(cacheDirectory, fileName);
                                File.Copy(library.Path, destination, true);
                                saved.Add(destination);
                            }

                            packageVersion.LibraryFiles = saved;
                            packageVersion.RepositoryFiles = repositoryFiles;
                            packageVersion.Compiled = reference.Compiled ? 1 : 0;
                            if (reference.IdentityKnown)
                                packageVersion.Title = reference.Title;
                            if (!string.IsNullOrEmpty(reference.Version))
                                packageVersion.Version = reference.Version;
                            if (!string.IsNullOrEmpty(reference.Distributor))
                                packageVersion.DistributorName = reference.Distributor;

                            if (extracted.Count > 1)
                                _logger.Info("[download] {0} ships {1} libraries; referencing {2}", packageVersion.Name, extracted.Count, reference.Title);
                            if (packageVersion.RepositoryFiles?.Count > 0)
                                _logger.Info("[download] {0} ships {1} repository file(s) (.tml/.tmx/.tme)", packageVersion.Name, packageVersion.RepositoryFiles.Count);
                        }
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(tempFilePath))
                            File.Delete(tempFilePath);
                        if (!string.IsNullOrEmpty(tempFolderPath))
                            Directory.Delete(tempFolderPath, true);
                    }
                }
            }

            _logger.Info("[download] {0} {1} (distributor: {2}) (from {3})", packageVersion.Title, packageVersion.Version, packageVersion.DistributorName, Url);
        }

        public virtual async Task<PackageVersionGetResponse> GetPackageVersionAsync(PlcLibrary library, string branch, string configuration, string target, CancellationToken cancellationToken = default)
        {
            if (_sourceRepository == null)
                return new PackageVersionGetResponse();

            PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();

            IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
                library.Name,
                includePrerelease: true,
                includeUnlisted: false,
                _cache,
                NullLogger.Instance,
                cancellationToken);

            var ordered = packages.OrderByDescending(p => p.Identity.Version);
            IPackageSearchMetadata x = library.Version == null
                ? ordered.FirstOrDefault()
                : packages.FirstOrDefault(p => VersionMatches(p.Identity.Version, library.Version));

            if (x == null)
                return new PackageVersionGetResponse();

            if (!x.Tags?.ToLower().Contains("library") == true && !x.Tags?.ToLower().Contains("plc-library") == true)
                throw new LibraryFileInvalidException($"Package {library.Name} {library.Version} (distributor: {library.DistributorName}) does not have a 'plc-library' or 'library' tag!");

            var dependencyPackages = x.DependencySets?.SelectMany(p => p.Packages).ToList() ?? new List<PackageDependency>();
            List<PackageVersionGetResponse> dependencies = new List<PackageVersionGetResponse>();

            foreach(var d in dependencyPackages)
            {
                var minVersion = d.VersionRange?.MinVersion?.Version;
                var minOriginalVersion = EvaluateVersion(d.VersionRange?.MinVersion);
                string version = null;
                if (minVersion != null)
                    version = (minVersion.Major == 0 && minVersion.Minor == 0 && minVersion.Revision == 0 && minVersion.Build == 0) ? null : minOriginalVersion;

                var dependencyMetadata = await resource.GetMetadataAsync(
                    d.Id,
                    includePrerelease: true,
                    includeUnlisted: false,
                    _cache,
                    NullLogger.Instance,
                    cancellationToken);
                var dependency = dependencyMetadata
                    .OrderByDescending(p => p.Identity.Version)
                    .FirstOrDefault(p => version == null || VersionMatches(p.Identity.Version, version));

                if (dependency == null && ResolveForeignDependencies)
                {
                    // not in this feed; leave branch, target and configuration open so other servers can resolve it
                    dependencies.Add(
                        new PackageVersionGetResponse()
                        {
                            Name = d.Id,
                            Title = d.Id,
                            DisplayName = d.Id,
                            Version = version,
                            Dependencies = null
                        });
                }
                else if(dependency?.Tags?.ToLower().Contains("library") == true || dependency?.Tags?.ToLower().Contains("plc-library") == true)
                {
                    dependencies.Add(
                        new PackageVersionGetResponse()
                        {
                            PackageId = null,
                            Name = dependency.Identity.Id,
                            Title = await EvaluateTitleAsync(dependency, cancellationToken),
                            DistributorName = dependency.Authors,
                            DisplayName = dependency.Identity.Id,
                            Description = dependency.Description,
                            Entitlement = null,
                            ProjectUrl = dependency.ProjectUrl?.ToString(),
                            IconUrl = dependency.IconUrl?.ToString() ?? IconUrl,
                            Authors = dependency.Authors,
                            License = dependency.LicenseMetadata?.License,
                            LicenseTmcBinary = null,
                            LicenseBinary = null,
                            Branches = new List<string>() { "main" },
                            Targets = new List<string>() { "TC3.1" },
                            Configurations = new List<string>() { "Release" },
                            Version = version?.ToString(),
                            Branch = "main",
                            Target = "TC3.1",
                            Configuration = "Release",
                            Compiled = EvaluateCompiled(dependency.Tags),
                            Released = dependency.Identity.Version.IsPrerelease == true ? 1 : 0,
                            Notes = dependency.Description,
                            //PackageType,
                            Binary = null,
                            BinaryDownloadUrl = null,
                            BinarySha256 = null,
                            Dependencies = null // we don't resolve dependencies of a dependency at this point
                        });
                }

            }

            var packageVersion = new PackageVersionGetResponse()
            {
                PackageId = null,
                Name = x.Identity.Id,
                Title = await EvaluateTitleAsync(x, cancellationToken),
                DistributorName = x.Authors,
                DisplayName = x.Identity.Id,
                Description = x.Description,
                Entitlement = null,
                ProjectUrl = x.ProjectUrl?.ToString(),
                IconUrl = x.IconUrl?.ToString() ?? IconUrl,
                Authors = x.Authors,
                License = x.LicenseMetadata?.License,
                LicenseTmcBinary = null,
                LicenseBinary = null,
                Branches = new List<string>() { "main" },
                Targets = new List<string>() { "TC3.1" },
                Configurations = new List<string>() { "Release" },
                Version = EvaluateVersion(x.Identity.Version),
                Branch = "main",
                Target = "TC3.1",
                Configuration = "Release",
                Compiled = EvaluateCompiled(x.Tags),
                Released = x.Identity.Version.IsPrerelease == true ? 1 : 0,
                Notes = x.Description,
                //PackageType,
                Binary = null,
                BinaryDownloadUrl = null,
                BinarySha256 = null,
                Dependencies = dependencies
            };
#if !TWINPACK_HEADLESS
            // NuGet packages created by Twinpack store the logo in the nupkg (<icon>), not as an
            // icon URL. An icon URL from the feed must not hide that file.
            packageVersion.IconBytes = await GetPackageIconAsync(x.Identity, cancellationToken);
#endif
            return packageVersion;
        }

        public async Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName, CancellationToken cancellationToken = default)
        {
            if (_sourceRepository == null)
                return new PackageGetResponse();

            PackageMetadataResource resource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();

            IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
                packageName,
                includePrerelease: true,
                includeUnlisted: false,
                _cache,
                NullLogger.Instance,
                cancellationToken);

            IPackageSearchMetadata x = packages.OrderByDescending(p => p.Identity.Version).FirstOrDefault();

            if (x == null)
                throw new Exceptions.LibraryNotFoundException(packageName, null, $"Package {packageName} (distributor: {distributorName}) not found");

            return new PackageGetResponse()
            {
                PackageId = null,
                Name = x.Identity.Id,
                Title = await EvaluateTitleAsync(x, cancellationToken),
                DistributorName = x.Authors,
                DisplayName = x.Identity.Id,
                Description = x.Description,
                Entitlement = null,
                ProjectUrl = x.ProjectUrl?.ToString(),
                IconUrl = x.IconUrl?.ToString() ?? IconUrl,
                Authors = x.Authors,
                License = x.LicenseMetadata?.License,
                LicenseTmcBinary = null,
                LicenseBinary = null,
                Branches = new List<string>() { "main" },
                Targets = new List<string>() { "TC3.1" },
                Configurations = new List<string>() { "Release" }
            };
        }

#pragma warning disable CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        public async Task<PackageVersionGetResponse> PutPackageVersionAsync(PackageVersionPatchRequest package, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        {
            throw new NotImplementedException();

        }

#pragma warning disable CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        public async Task<PackageGetResponse> PutPackageAsync(PackagePatchRequest package, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.

        {
            throw new NotImplementedException();

        }

        public async Task<LoginPostResponse> LoginAsync(string username = null, string password = null, CancellationToken cancellationToken = default)
        {
            var storePassword = !string.IsNullOrEmpty(password);
            InvalidateCache();
            UserInfo = new LoginPostResponse();
            if (!Enabled)
            {
                return UserInfo;
            }

            try
            {
                var credentials = CredentialManager.GetCredentials(UrlBase);
                Username = username ?? credentials?.UserName ?? "";
                Password = password ?? credentials?.Password ?? "";
            }
            catch (Exception ex)
            {
                _logger.Warn("[login] failed to load credentials");
                _logger.Trace(ex);
                Username = username ?? "";
                Password = password ?? "";
            }

            // reset token to get a new one
            if (UserInfo?.Token != null)
                UserInfo.Token = null;

            try
            {
                PackageSource packageSource = new PackageSource(Url) { Credentials = !string.IsNullOrEmpty(Password) ? new PackageSourceCredential(Url, Username ?? "", Password, true, null) : null };
                _sourceRepository = CreateSourceRepository(packageSource);

                // No need to check if we can login to local repositories
                if (!Path.IsPathRooted(Url))
                {
                    using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
                    {
                        // A blank username with a password is an API key. The key is the Basic password, so the header is ":" + key.
                        if (!string.IsNullOrEmpty(Password))
                            httpClient.DefaultRequestHeaders.Authorization = BasicAuthorization(Username, Password);

                        var response = await httpClient.GetAsync(Url.EndsWith("/") ? Url : Url + "/", cancellationToken);

                        if (!response.IsSuccessStatusCode)
                            throw new LoginException($"Login failed, status code: {response.StatusCode}");
                    };
                }

                UserInfo = new LoginPostResponse() { User = Username };

                if (!string.IsNullOrEmpty(Password))
                    UserInfo.Configurations = new List<LoginPostResponse.Configuration> { new LoginPostResponse.Configuration { Public = 0 } };

                if (storePassword)
                {
                    try
                    {
                        CredentialManager.SaveCredentials(UrlBase, new System.Net.NetworkCredential(Username, Password));
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn("[login] failed to save credentials");
                        _logger.Trace(ex);
                    }
                }

                _logger.Info("[login] successful: {0}", UrlBase);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (LoginException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new LoginException(ex.InnerException?.Message ?? ex.Message);
            }
            catch (Exception ex) when (CancelableTask.IsCancellation(ex))
            {
                throw;
            }
            catch
            {
                DeleteCredential();
                throw;
            }

            return UserInfo;
        }

        private void DeleteCredential()
        {
            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";
            try
            {
                CredentialManager.RemoveCredentials(UrlBase);
            }
            catch { }
        }

#pragma warning disable CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        public async Task LogoutAsync()
#pragma warning restore CS1998 // Bei der asynchronen Methode fehlen "await"-Operatoren. Die Methode wird synchron ausgeführt.
        {

            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";

            try
            {
                if (CredentialManager.GetCredentials(UrlBase) != null)
                {
                    _logger.Info("[login] removing stored credentials for {0}", UrlBase);
                    CredentialManager.RemoveCredentials(UrlBase);
                }
            }
            catch (Exception) { }
        }

        public void InvalidateCache()
        {
            _cache = new SourceCacheContext { };
            _noCache = new SourceCacheContext { NoCache = true, DirectDownload = true };
        }

        protected virtual async Task<string> EvaluateTitleAsync(IPackageSearchMetadata package, CancellationToken cancellationToken)
        {
            try
            {
                PackageIdentity identity = package.Identity;
                FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

                using (MemoryStream packageStream = new MemoryStream())
                using (var memoryStream = new MemoryStream())
                {
                    await resource.CopyNupkgToStreamAsync(
                        identity.Id,
                        identity.Version,
                        packageStream,
                        _cache,
                        NullLogger.Instance,
                        cancellationToken);

                    using (PackageArchiveReader packageReader = new PackageArchiveReader(packageStream))
                    {
                        if(!string.IsNullOrEmpty(packageReader.NuspecReader.GetTitle()))
                            return packageReader.NuspecReader.GetTitle();
                    }
                }
            }
            catch (Exception ex) when (CancelableTask.IsCancellation(ex) || cancellationToken.IsCancellationRequested)
            {
                if (ex is OperationCanceledException)
                    throw;

                throw new OperationCanceledException("The operation was canceled.", ex, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error("Can't unpack package icon:" + ex);
            }


            if (!string.IsNullOrEmpty(package.Title))
                return package.Title;

            return package.Identity.Id;
        }

        /// <summary>
        /// Version as the rest of Twinpack spells it. The version a NuGet feed reports needs no
        /// translation, since NuGet keeps all four parts, so this only decodes the superseded
        /// convention that carried the revision in the prerelease. A three part version is left as
        /// it is, because TwinCAT accepts one. This value ends up in config.json, in library file
        /// names and in requests to the other configured package servers.
        /// </summary>
        protected virtual string EvaluateVersion(NuGetVersion version)
        {
            return Core.AutomationInterface.FourPartVersion(version?.OriginalVersion ?? version?.ToString());
        }

        // NuGet normalizes a trailing ".0" away, so "1.0.0.0-feat-ci" has to match a package stored as
        // "1.0.0-feat-ci"; packages published under the superseded convention may also carry the 4th
        // part in the prerelease ("1.0.0-feat-ci.1" is "1.0.0.1-feat-ci")
        protected bool VersionMatches(NuGetVersion candidate, string requested)
        {
            if (EvaluateVersion(candidate) == requested)
                return true;

            return NuGetVersion.TryParse(Core.AutomationInterface.FourPartVersion(candidate.OriginalVersion ?? candidate.ToString()), out var left)
                && NuGetVersion.TryParse(Core.AutomationInterface.FourPartVersion(requested), out var right)
                && VersionComparer.Default.Equals(left, right);
        }

        static async Task ExtractPackageFileAsync(PackageArchiveReader packageReader, string relativePath, string destination)
        {
            var entry = packageReader.GetEntry(relativePath);
            if (entry == null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (var input = entry.Open())
            using (var output = File.Create(destination))
                await input.CopyToAsync(output);
        }

        static bool IsRepositoryFile(string path)
        {
            var extension = Path.GetExtension(path ?? "");
            return extension.Equals(".tmx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tml", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tme", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Searches used when listing a feed for mirror. Beckhoff adds the TMX tag, because those
        /// packages are not tagged as libraries.
        /// </summary>
        protected virtual IEnumerable<string> MirrorListPrefixes()
        {
            yield return SearchPrefix;
        }

        /// <summary>
        /// The MSI installs platform images under CommonAppData\Beckhoff\TwinCAT\3.1\Repository.
        /// InstallLibrary does not. Keep that tree, rooted at <c>3.1\Repository</c>, so install can
        /// copy it to the same place. A mirrored package stores the same relative path under
        /// <c>repository/</c>.
        /// </summary>
        static List<string> CopyRepositoryFiles(string extractedRoot, string cacheDirectory, PackageVersionGetResponse packageVersion)
        {
            var saved = new List<string>();
            if (string.IsNullOrEmpty(extractedRoot) || !Directory.Exists(extractedRoot))
                return saved;

            var repositoryRoot = Path.Combine(cacheDirectory, $"{SafeFileName(packageVersion.Name)}_{packageVersion.Version}.repository");
            foreach (var file in Directory.GetFiles(extractedRoot, "*", SearchOption.AllDirectories))
            {
                if (!IsRepositoryFile(file))
                    continue;

                var normalized = file.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                var relative = RepositoryRelativePath(normalized);
                if (relative == null)
                    continue;

                var destination = Path.Combine(repositoryRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
                saved.Add(destination);
            }

            return saved;
        }

        static string RepositoryRelativePath(string normalizedPath)
        {
            var markers = new[] { @"\3.1\Repository\", @"\repository\" };
            foreach (var marker in markers)
            {
                var index = normalizedPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                    return normalizedPath.Substring(index + marker.Length);
            }

            return null;
        }

        static string SafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "library";

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrEmpty(cleaned) ? "library" : cleaned;
        }

        protected virtual int EvaluateCompiled(string tags)
        {
            var tagList = tags.Split(' ').ToList();
            return tagList.IndexOf("tp-compiled-library") >= 0 ? 1 : 0;
        }

        const int MirrorSearchPageSize = 100;

        /// <summary>
        /// Every package id on this feed. A NuGet catalog (<c>Catalog/3.0.0</c>) is used when the
        /// service index advertises one, because search stops after about 1000 hits. Otherwise the
        /// ids come from search. Beckhoff keeps its <c>tags:library</c> prefix. Ids are not filtered
        /// down to the library tag the catalog view uses.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListPackageIdsAsync(CancellationToken cancellationToken = default)
        {
            EnsureRepository();

            try
            {
                var fromCatalog = await ListPackageIdsFromCatalogAsync(cancellationToken);
                if (fromCatalog != null)
                    return fromCatalog;
            }
            catch (Exception ex) when (!CancelableTask.IsCancellation(ex))
            {
                _logger.Warn("[mirror] catalog listing failed for {0}, falling back to search: {1}", UrlBase, ex.Message);
                _logger.Trace(ex);
            }

            return await ListPackageIdsFromSearchAsync(cancellationToken);
        }

        /// <summary>
        /// Package ids for one requested name. An exact feed id is used when it exists. Otherwise
        /// search matches the id or its last segment, so <c>Tc2_Standard</c> finds
        /// <c>TwinCAT.XAE.PLC.Lib.Tc2_Standard</c>.
        /// </summary>
        public async Task<IReadOnlyList<string>> FindPackageIdsAsync(string name, CancellationToken cancellationToken = default)
        {
            EnsureRepository();
            if (string.IsNullOrWhiteSpace(name))
                return new List<string>();

            var ids = new List<string>();
            var exact = await ListPackageVersionsAsync(name, cancellationToken);
            if (exact.Count > 0)
                ids.Add(name);

            foreach (var prefix in MirrorListPrefixes())
            {
                var page = (await SearchAsync(prefix, name, 0, 40, cancellationToken))?.ToList()
                    ?? new List<IPackageSearchMetadata>();
                foreach (var package in page)
                {
                    var id = package?.Identity?.Id;
                    if (string.IsNullOrEmpty(id) || !PackageIdMatches(id, name))
                        continue;
                    if (ids.Any(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    ids.Add(id);
                }
            }

            return ids;
        }

        static bool PackageIdMatches(string id, string name)
        {
            if (string.Equals(id, name, StringComparison.OrdinalIgnoreCase))
                return true;

            var last = id.LastIndexOf('.');
            var segment = last >= 0 ? id.Substring(last + 1) : id;
            return string.Equals(segment, name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Every version of one package. An unknown id returns an empty list.</summary>
        public async Task<IReadOnlyList<NuGetVersion>> ListPackageVersionsAsync(string packageId, CancellationToken cancellationToken = default)
        {
            EnsureRepository();
            if (string.IsNullOrWhiteSpace(packageId))
                return new List<NuGetVersion>();

            FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            var versions = await resource.GetAllVersionsAsync(packageId, _noCache, NullLogger.Instance, cancellationToken);
            return versions?.ToList() ?? new List<NuGetVersion>();
        }

        /// <summary>Writes the .nupkg bytes for one version. The stream is left as the caller gave it.</summary>
        public async Task DownloadNupkgAsync(string packageId, NuGetVersion version, Stream destination, CancellationToken cancellationToken = default)
        {
            EnsureRepository();
            if (string.IsNullOrWhiteSpace(packageId) || version == null)
                throw new ArgumentException("A package id and version are required to download a nupkg");
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            await resource.CopyNupkgToStreamAsync(
                packageId,
                version,
                destination,
                _noCache,
                NullLogger.Instance,
                cancellationToken);
        }

        /// <summary>
        /// Publishes one .nupkg. The stored password is sent as <c>X-NuGet-ApiKey</c>. A package the
        /// target already has (HTTP 409) is not a failure.
        /// </summary>
        public async Task PushNupkgAsync(string nupkgPath, CancellationToken cancellationToken = default)
        {
            EnsureRepository();
            if (string.IsNullOrWhiteSpace(nupkgPath))
                throw new ArgumentException("A nupkg path is required to push");

            var resource = await _sourceRepository.GetResourceAsync<PackageUpdateResource>(cancellationToken);
            await resource.Push(
                new List<string> { nupkgPath },
                symbolSource: null,
                timeoutInSecond: 600,
                disableBuffering: false,
                getApiKey: _ => Password ?? "",
                getSymbolApiKey: _ => null,
                noServiceEndpoint: false,
                skipDuplicate: true,
                symbolPackageUpdateResource: null,
                log: new TraceNuGetLogger(_logger));
        }

        void EnsureRepository()
        {
            if (_sourceRepository == null)
                throw new ProtocolException($"Package server '{Name}' is not logged in");
        }

        async Task<IReadOnlyList<string>> ListPackageIdsFromCatalogAsync(CancellationToken cancellationToken)
        {
            ServiceIndexResourceV3 index = null;
            try
            {
                index = await _sourceRepository.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);
            }
            catch (Exception ex) when (!CancelableTask.IsCancellation(ex))
            {
                _logger.Trace(ex);
                return null;
            }

            // 6.11's ServiceTypes has no public Catalog field. The service index type is still this.
            var catalogUri = index?.GetServiceEntryUri("Catalog/3.0.0");
            if (catalogUri == null)
                return null;

            if (!catalogUri.IsAbsoluteUri && Uri.TryCreate(UrlBase, UriKind.Absolute, out var feed))
                catalogUri = new Uri(feed, catalogUri);

            _logger.Info("[mirror] {0}: listing package ids from the NuGet catalog", Name ?? UrlBase);

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<Uri>();
            var seenPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pending.Enqueue(catalogUri);

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                if (!string.IsNullOrEmpty(Password))
                    http.DefaultRequestHeaders.Authorization = BasicAuthorization(Username, Password);

                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = pending.Dequeue();
                    if (page == null || !seenPages.Add(page.AbsoluteUri))
                        continue;

                    using (var response = await http.GetAsync(page, cancellationToken))
                    {
                        response.EnsureSuccessStatusCode();
                        var json = await response.Content.ReadAsStringAsync();
                        ReadCatalogPage(json, ids, pending);
                    }
                }
            }

            return ids.ToList();
        }

        static void ReadCatalogPage(string json, HashSet<string> ids, Queue<Uri> pending)
        {
            using (var document = JsonDocument.Parse(json))
            {
                if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || IsCatalogDelete(item))
                        continue;

                    var packageId = JsonString(item, "nuget:id");
                    if (!string.IsNullOrEmpty(packageId))
                    {
                        ids.Add(packageId);
                        continue;
                    }

                    var next = JsonString(item, "@id");
                    if (Uri.TryCreate(next, UriKind.Absolute, out var nextUri))
                        pending.Enqueue(nextUri);
                }
            }
        }

        static bool IsCatalogDelete(JsonElement item)
        {
            if (!item.TryGetProperty("@type", out var type))
                return false;

            if (type.ValueKind == JsonValueKind.String)
                return IsDeleteType(type.GetString());

            if (type.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in type.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String && IsDeleteType(entry.GetString()))
                    return true;
            }

            return false;
        }

        static bool IsDeleteType(string type)
        {
            return type != null && type.IndexOf("PackageDelete", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string JsonString(JsonElement item, string name)
        {
            if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
                return null;

            return value.GetString();
        }

        async Task<IReadOnlyList<string>> ListPackageIdsFromSearchAsync(CancellationToken cancellationToken)
        {
            _logger.Info("[mirror] {0}: listing package ids from search", Name ?? UrlBase);

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prefix in MirrorListPrefixes())
            {
                var skip = 0;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = (await SearchAsync(prefix, "", skip, MirrorSearchPageSize, cancellationToken))?.ToList()
                        ?? new List<IPackageSearchMetadata>();
                    if (page.Count == 0)
                        break;

                    var added = 0;
                    foreach (var package in page)
                    {
                        var id = package?.Identity?.Id;
                        if (!string.IsNullOrEmpty(id) && ids.Add(id))
                            added++;
                    }

                    // A feed that ignores skip would otherwise repeat the same page forever.
                    if (added == 0 || page.Count < MirrorSearchPageSize)
                        break;

                    skip += MirrorSearchPageSize;
                }
            }

            return ids.ToList();
        }

        // PackageSourceCredential.IsValid() is false when the username is empty, and NuGet.Protocol then
        // ignores the credential. An API key is that case: blank username, key in the password field.
        private static SourceRepository CreateSourceRepository(PackageSource packageSource)
        {
            var providers = Repository.Provider.GetCoreV3().ToList();
            providers.Add(new Lazy<INuGetResourceProvider>(() => new ApiKeyHttpHandlerResourceProvider()));
            return new SourceRepository(packageSource, providers);
        }

        private static AuthenticationHeaderValue BasicAuthorization(string username, string password)
        {
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username ?? ""}:{password}"));
            return new AuthenticationHeaderValue("Basic", token);
        }

        private sealed class ApiKeyHttpHandlerResourceProvider : ResourceProvider
        {
            private readonly HttpHandlerResourceV3Provider _fallback = new HttpHandlerResourceV3Provider();

            public ApiKeyHttpHandlerResourceProvider()
                : base(typeof(HttpHandlerResource), nameof(ApiKeyHttpHandlerResourceProvider), nameof(HttpHandlerResourceV3Provider))
            {
            }

            public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken cancellationToken)
            {
                var credentials = source.PackageSource.Credentials;
                if (credentials == null || credentials.IsValid() || string.IsNullOrEmpty(credentials.Password))
                    return new Tuple<bool, INuGetResource>(false, null);

                var created = await _fallback.TryCreate(source, cancellationToken);
                if (created == null || !created.Item1 || !(created.Item2 is HttpHandlerResourceV3 http))
                    return new Tuple<bool, INuGetResource>(false, null);

                var handler = new PreemptiveBasicAuthHandler(http.MessageHandler, credentials.Username, credentials.Password);
                return new Tuple<bool, INuGetResource>(true, new HttpHandlerResourceV3(http.ClientHandler, handler));
            }
        }

        /// <summary>
        /// NuGet push progress is trace, so it shows on the console only with --verbose.
        /// Warnings and errors stay visible on a normal run.
        /// </summary>
        private sealed class TraceNuGetLogger : LoggerBase
        {
            private readonly NLog.Logger _log;

            public TraceNuGetLogger(NLog.Logger log)
            {
                _log = log;
            }

            public override void Log(ILogMessage message)
            {
                var text = message?.Message;
                if (string.IsNullOrEmpty(text))
                    return;

                if (message.Level == LogLevel.Warning)
                    _log.Warn("[push] {0}", text);
                else if (message.Level == LogLevel.Error)
                    _log.Error("[push] {0}", text);
                else
                    _log.Trace("[push] {0}", text);
            }

            public override Task LogAsync(ILogMessage message)
            {
                Log(message);
                return Task.CompletedTask;
            }
        }

        private sealed class PreemptiveBasicAuthHandler : DelegatingHandler
        {
            private readonly AuthenticationHeaderValue _authorization;

            public PreemptiveBasicAuthHandler(HttpMessageHandler inner, string username, string password)
                : base(inner)
            {
                _authorization = BasicAuthorization(username, password);
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.Authorization = _authorization;
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
