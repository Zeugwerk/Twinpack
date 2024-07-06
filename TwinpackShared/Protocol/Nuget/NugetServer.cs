using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Models;
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
using System.Data;

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

        public virtual async Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            if(_sourceRepository == null)
                return new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(new List<CatalogItemGetResponse>(), false);

            ILogger logger = NullLogger.Instance;

            try
            {
                PackageSearchResource resource = await _sourceRepository.GetResourceAsync<PackageSearchResource>(cancellationToken);

                IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
                    SearchPrefix + search,
                    new SearchFilter(includePrerelease: true),
                    skip: perPage * (page - 1),
                    take: perPage,
                    logger,
                    cancellationToken);

                return
                    new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(
                        results.Select(x =>
                            new CatalogItemGetResponse()
                            {
                                PackageId = null,
                                Name = x.Identity.Id,
                                DistributorName = x.Authors,
                                // Description = x.Description, Beckhoff's descriptions are meh
                                IconUrl = x.IconUrl?.ToString() ?? IconUrl,
                                RuntimeLicense = 1,
                                DisplayName = x.Identity.Id,
                                Downloads = (int)x.DownloadCount,
                                Created = x.Published?.ToString(),
                                Modified = x.Published?.ToString()
                            }).ToList(),
                        results.Any());
            }
            catch(Exception)
            {
                throw;
            }

        }

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
                        Title = EvaluateTitle(x),
                        DistributorName = x.Authors,
                        DisplayName = x.Identity.Id,
                        // Description = x.Description,
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
                        Compiled = 1,
                        //Notes =,
                        //PackageType,
                        Binary = null,
                        BinaryDownloadUrl = null,
                        BinarySha256 = null
                    }).ToList(), false);
        }

        public virtual async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default)
        {
            return await GetPackageVersionAsync(library, preferredBranch, preferredConfiguration, preferredTarget, cancellationToken);
        }

        public async Task DownloadPackageVersionAsync(PackageVersionGetResponse packageVersion, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default)
        {
            if (_sourceRepository == null)
                return;

            FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();

            using (MemoryStream packageStream = new MemoryStream())
            {
                var version = new NuGetVersion(packageVersion.Version);
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
                    _cache,
                    NullLogger.Instance,
                    cancellationToken);

                using (PackageArchiveReader packageReader = new PackageArchiveReader(packageStream))
                {
                    NuspecReader nuspecReader = await packageReader.GetNuspecReaderAsync(cancellationToken);
                    var packageFiles = await packageReader.GetPackageFilesAsync(PackageSaveMode.Files, cancellationToken);

                    var msis = packageFiles.Where(x => x.EndsWith(".msi", StringComparison.InvariantCultureIgnoreCase));
                    var libraries = packageFiles.Where(x => 
                        x.EndsWith(".library", StringComparison.InvariantCultureIgnoreCase) ||
                        Path.GetExtension(x).StartsWith(".compiled-library"));

                    if(libraries.Count() == 1)
                    {
                        var library = libraries.First();
                        var entry = packageReader.GetEntry(library);
                        if (entry != null)
                        {
                            try
                            {
                                var extension = library.EndsWith(".library") ? "compiled-library" : "library";
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

                                var files = Directory.GetFiles(tempFolderPath, "*", SearchOption.AllDirectories).Where(x => Path.GetExtension(x) == ".library" || Path.GetExtension(x).StartsWith(".compiled-library"));
                                if(files.Count() != 1)
                                {
                                    throw new Exceptions.GetException("nupkg contains a msi file that contains more than one library!");
                                }
                                foreach (var f in files)
                                {
                                    var extension = Path.GetExtension(f).StartsWith(".compiled-library") ? "compiled-library" : "library";
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
                        throw new Exceptions.GetException("nupkg should contain a single msi or library file!");
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
                throw new Exceptions.LibraryNotFoundException(library.Name, library.Version, $"Package {library.Name} (version: {library.Version}, distributor: {library.DistributorName}) not found!");

            if (!x.Tags?.ToLower().Contains("library") == true)
                throw new Exceptions.LibraryFileInvalidException($"Package {library.Name} (version: {library.Version}, distributor: {library.DistributorName}) does not have a 'Library' tag!");

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

                if(dependency?.Tags?.ToLower().Contains("library") == true)
                {
                    dependencies.Add(
                        new PackageVersionGetResponse()
                        {
                            PackageId = null,
                            Name = dependency.Identity.Id,
                            Title = EvaluateTitle(dependency),
                            DistributorName = x.Authors,
                            DisplayName = dependency.Identity.Id,
                            // Description = dependency.Description,
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
                            Compiled = 1,
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
                Title = EvaluateTitle(x),
                DistributorName = x.Authors,
                DisplayName = x.Identity.Id,
                // Description = x.Description,
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
                Compiled = 1,
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
                Title = EvaluateTitle(x),
                DistributorName = x.Authors,
                DisplayName = x.Identity.Id,
                // Description = x.Description,
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
            var credentials = CredentialManager.GetCredentials(UrlBase);
            
            Username = username ?? credentials?.UserName;
            Password = password ?? credentials?.Password;

            // reset token to get a new one
            if (UserInfo?.Token != null)
                UserInfo.Token = null;

            try
            {
                PackageSource packageSource = new PackageSource(Url) { Credentials = Username != null ? new PackageSourceCredential(Url, Username, Password, true, null) : null };

                _sourceRepository = Repository.Factory.GetCoreV3(packageSource);
                var resource = await _sourceRepository.GetResourceAsync<PackageSearchResource>();
                IEnumerable<IPackageSearchMetadata> results = await resource.SearchAsync(
                    "",
                    new SearchFilter(includePrerelease: true),
                    skip: 0,
                    take: 1,
                    NullLogger.Instance,
                    cancellationToken);

                UserInfo = new LoginPostResponse() { User = Username };

                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                    CredentialManager.SaveCredentials(Url, new System.Net.NetworkCredential(Username, Password));
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
            _logger.Trace("Log out from NuGet Server");

            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";

            try
            {
                CredentialManager.RemoveCredentials(UrlBase);
            }
            catch (Exception) { }
        }

        public void InvalidateCache()
        {
            _cache = new SourceCacheContext();
        }

        private string EvaluateTitle(IPackageSearchMetadata x)
        {
            // heuristics for the actual title of the package, needed for Beckhoff, because there is no metadata, which gives the real name of the library
            var title = x.Identity.Id;
            var tags = x.Tags.Split(' ');
            var libraryIdx = tags.ToList().IndexOf("Library");
            if (libraryIdx > 0 && tags.Length > libraryIdx + 1 && title.Contains(tags[libraryIdx + 1]))
                title = tags[libraryIdx + 1];

            return title;
        }
    }
}
