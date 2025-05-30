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
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        public string ServerType { get; } = "NuGet Repository";
        public string Name { get; set; }
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
        public bool LoggedIn { get { return Connected; } }
        public bool Connected { get { return UserInfo?.User != null; } }
        protected virtual string SearchPrefix { get => "";}
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

        private async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchPrefix, string search, int skip, int take, CancellationToken cancellationToken)
        {
            ILogger logger = NullLogger.Instance;
            PackageSearchResource resource = await _sourceRepository.GetResourceAsync<PackageSearchResource>(cancellationToken);

            return await resource.SearchAsync(
                searchPrefix + search.Replace(" ", "_"),
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
#if !NETSTANDARD2_1_OR_GREATER
                                ,Icon = await GetPackageIconAsync(x.Identity, cancellationToken),
#endif
                            }));

                return new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(packages, results.Any());
            }
            catch(Exception)
            {
                throw;
            }

        }

#if !NETSTANDARD2_1_OR_GREATER
        protected virtual async Task<System.Windows.Media.Imaging.BitmapImage> GetPackageIconAsync(PackageIdentity identity, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var cache in new List<SourceCacheContext> { _cache, _noCache })
                {
                    FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();
                    using (MemoryStream packageStream = new MemoryStream())
                    using (var memoryStream = new MemoryStream())
                    {
                        await resource.CopyNupkgToStreamAsync(
                            identity.Id,
                            identity.Version,
                            packageStream,
                            _noCache,
                            NullLogger.Instance,
                            cancellationToken);

                        using (PackageArchiveReader packageReader = new PackageArchiveReader(packageStream))
                        {
                            var iconPath = packageReader.NuspecReader.GetIcon();
                            if (iconPath == null)
                            {
                                return null;
                            }

                            try
                            {
                                var zipEntry = packageReader.GetEntry(iconPath);
                                await zipEntry.Open().CopyToAsync(memoryStream);
                                memoryStream.Position = 0;

                                if (zipEntry != null)
                                {
                                    var image = new System.Windows.Media.Imaging.BitmapImage();
                                    image.BeginInit();
                                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                    image.StreamSource = memoryStream;
                                    image.EndInit();
                                    image.Freeze();
                                    return image;
                                }

                                return null;
                            }
                            catch (Exception ex) { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error("Can't unpack package icon:" + ex);
            }


            return null;
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
                    results.Select(x => new PackageVersionGetResponse()
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
                        Version = x.Identity.Version.Version.ToString(),
                        Branch = "main",
                        Target = "TC3.1",
                        Configuration = "Release",
                        Compiled = EvaluateCompiled(x.Tags),
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
                var version = packageVersion.Version == null ? null : new NuGetVersion(packageVersion.Version);
                if (version == null)
                {
                    PackageMetadataResource meta = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
                    IEnumerable<IPackageSearchMetadata> metaData = await meta.GetMetadataAsync(
                        packageVersion.Name,
                        includePrerelease: true,
                        includeUnlisted: false,
                        _cache,
                        NullLogger.Instance,
                        cancellationToken);
                    version = metaData.FirstOrDefault()?.Identity.Version;
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
                    NuspecReader nuspecReader = await packageReader.GetNuspecReaderAsync(cancellationToken);
                    var packageFiles = await packageReader.GetPackageFilesAsync(PackageSaveMode.Files, cancellationToken);

                    var msis = packageFiles.Where(x => x.EndsWith(".msi", StringComparison.InvariantCultureIgnoreCase));
                    var libraries = packageFiles.Where(x => 
                        (packageVersion.Compiled == 0 && x.EndsWith(".library", StringComparison.InvariantCultureIgnoreCase)) ||
                        (packageVersion.Compiled == 1 && Path.GetExtension(x).StartsWith(".compiled-library")));

                    if(libraries.Count() == 1)
                    {
                        var library = libraries.First();
                        var entry = packageReader.GetEntry(library);
                        if (entry != null)
                        {
                            try
                            {
                                var extension = packageVersion.Compiled == 1 ? "compiled-library" : "library";
                                var filePath = $@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}";
                                var fileName = $@"{filePath}\{packageVersion.Name}_{packageVersion.Version}.{extension}";
                                Directory.CreateDirectory(filePath);

                                using (var file = File.OpenWrite(fileName))
                                {
                                    await entry.Open().CopyToAsync(file);
                                }
                            }
                            finally
                            {
                            }
                        }
                    }
                    else if (msis.Count() == 1)
                    {
                        var entry = packageReader.GetEntry(msis.First());
                        if (entry != null)
                        {
                            string tempFilePath = Path.GetTempFileName();
                            string tempFolderPath = Path.GetTempFileName().Replace(".tmp", "");
                            try
                            {
                                Directory.CreateDirectory(tempFolderPath);

                                using (var file = File.OpenWrite(tempFilePath))
                                {
                                    await entry.Open().CopyToAsync(file);
                                }
                                using (var pkg = new InstallPackage(tempFilePath, DatabaseOpenMode.ReadOnly))
                                {
                                    pkg.WorkingDirectory = tempFolderPath;
                                    pkg.ExtractFiles();
                                }

                                var files = Directory.GetFiles(tempFolderPath, "*", SearchOption.AllDirectories)
                                    .Where(x => 
                                    (packageVersion.Compiled == 0 && Path.GetExtension(x) == ".library") || 
                                    (packageVersion.Compiled == 1 && Path.GetExtension(x).StartsWith(".compiled-library")));

                                if(files.Count() != 1)
                                {
                                    throw new Exceptions.ProtocolException("nupkg contains a msi file that contains more than one library!");
                                }
                                foreach (var f in files)
                                {
                                    var extension = packageVersion.Compiled == 1 ? "compiled-library" : "library";
                                    var filePath = $@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}";
                                    var fileName = $@"{filePath}\{packageVersion.Name}_{packageVersion.Version}.{extension}";
                                    Directory.CreateDirectory(filePath);
                                    File.Copy(f, fileName, true);
                                }
                            }
                            finally
                            {
                                File.Delete(tempFilePath);
                                Directory.Delete(tempFolderPath, true);
                            }
                        }
                    }
                    else
                    {
                        throw new Exceptions.ProtocolException($"nupkg should contain a single .msi or {(packageVersion.Compiled == 1 ? ".compiled-library" : ".library")} file!");
                    }
                }
            }

            _logger.Info($"Downloaded {packageVersion.Title} {packageVersion.Version} (distributor: {packageVersion.DistributorName}) (from {Url})");
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

            IPackageSearchMetadata x = library.Version == null ? packages.FirstOrDefault() : packages.FirstOrDefault(p => p.Identity.Version.Version.ToString() == library.Version);

            if (x == null)
                return new PackageVersionGetResponse();

            if (!x.Tags?.ToLower().Contains("library") == true && !x.Tags?.ToLower().Contains("plc-library") == true)
                throw new LibraryFileInvalidException($"Package {library.Name} {library.Version} (distributor: {library.DistributorName}) does not have a 'plc-library' or 'library' tag!");

            var dependencyPackages = x.DependencySets?.SelectMany(p => p.Packages).ToList() ?? new List<PackageDependency>();
            List<PackageVersionGetResponse> dependencies = new List<PackageVersionGetResponse>();

            foreach(var d in dependencyPackages)
            {
                var version = d.VersionRange?.MinVersion?.Version;
                version = (version.Major == 0 && version.Minor == 0 && version.Revision == 0 && version.Build == 0) ? null : version;

                var dependency = (await resource.GetMetadataAsync(
                    d.Id,
                    includePrerelease: true,
                    includeUnlisted: false,
                    _cache,
                    NullLogger.Instance,
                    cancellationToken)).FirstOrDefault(p => version == null || version.ToString() == p.Identity.Version.ToString());

                if(dependency?.Tags?.ToLower().Contains("library") == true || dependency?.Tags?.ToLower().Contains("plc-library") == true)
                {
                    dependencies.Add(
                        new PackageVersionGetResponse()
                        {
                            PackageId = null,
                            Name = dependency.Identity.Id,
                            Title = await EvaluateTitleAsync(dependency, cancellationToken),
                            DistributorName = x.Authors,
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
                            Notes = dependency.Description,
                            //PackageType,
                            Binary = null,
                            BinaryDownloadUrl = null,
                            BinarySha256 = null,
                            Dependencies = null // we don't resolve dependencies of a dependency at this point
                        });
                }

            }

            return new PackageVersionGetResponse()
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
                Version = x.Identity.Version.Version.ToString(),
                Branch = "main",
                Target = "TC3.1",
                Configuration = "Release",
                Compiled = EvaluateCompiled(x.Tags),
                Notes = x.Description,
                //PackageType,
                Binary = null,
                BinaryDownloadUrl = null,
                BinarySha256 = null,
                Dependencies = dependencies
            };
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

            IPackageSearchMetadata x = packages.FirstOrDefault();

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
            InvalidateCache();
            try
            {
                var credentials = CredentialManager.GetCredentials(UrlBase);
                Username = username ?? credentials?.UserName;
                Password = password ?? credentials?.Password;
            }
            catch (Exception ex)
            {
                _logger.Warn("Failed to load credentials");
                _logger.Trace(ex);
                Username = username;
                Password = password;
            }

            // reset token to get a new one
            if (UserInfo?.Token != null)
                UserInfo.Token = null;

            try
            {
                PackageSource packageSource = new PackageSource(Url) { Credentials = Username != null ? new PackageSourceCredential(Url, Username, Password, true, null) : null };

                _sourceRepository = Repository.Factory.GetCoreV3(packageSource);
                var results = await SearchAsync("", "", 0, 1, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                UserInfo = new LoginPostResponse() { User = Username };

                if (!string.IsNullOrEmpty(Password))
                {
                    try
                    {
                        CredentialManager.SaveCredentials(UrlBase, new System.Net.NetworkCredential(Username, Password));

                    }
                    catch (Exception ex)
                    {
                        _logger.Warn("Failed to save credentials");
                        _logger.Trace(ex);
                    }
                }

                _logger.Info($"Log in to '{UrlBase}' successful");
            }
            catch(FatalProtocolException ex)
            {
                _logger.Trace(ex);
                DeleteCredential();
                throw new LoginException(ex.Message);
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
                    _logger.Info($"Removing existing credentials for {UrlBase}");
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
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error("Can't unpack package icon:" + ex);
            }


            if (!string.IsNullOrEmpty(package.Title))
                return package.Title;

            return package.Identity.Id;
        }

        protected virtual int EvaluateCompiled(string tags)
        {
            var tagList = tags.Split(' ').ToList();
            return tagList.IndexOf("tp-compiled-library") >= 0 ? 1 : 0;
        }
    }
}
