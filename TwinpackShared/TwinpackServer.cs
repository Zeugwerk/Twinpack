using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Twinpack.Models;
using Twinpack.Exceptions;
using System.Reflection;
using AdysTech.CredentialManager;
using System.Threading;
using System.Security.Cryptography;

namespace Twinpack
{
    public class TwinpackServer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        private CachedHttpClient _client = new CachedHttpClient();
        private string _token = string.Empty;

        public string TwinpackUrlBase = "https://twinpack.dev";
        public string TwinpackUrl = "https://twinpack.dev/index.php";
        public string RegisterUrl = "https://twinpack.dev/wp-login.php";

        public string Username { get; set; }
        public string Password { get; set; }
        public LoginPostResponse UserInfo { get; set; }
        public bool LoggedIn { get { return UserInfo?.User != null; } }

        public enum ChecksumMode
        {
            IgnoreMismatch,
            IgnoreMismatchAndFallack,
            Throw
        }

        public TwinpackServer()
        {
            var credentials = CredentialManager.GetCredentials(TwinpackUrlBase);
            Username = credentials?.UserName;
            Password = credentials?.Password;
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

        public void AddHeaders(System.Net.Http.HttpRequestMessage request)
        {
            request.Headers.Add("zgwk-username", Username ?? Environment.GetEnvironmentVariable("ZGWK_TWINPACK_USER"));
            request.Headers.Add("zgwk-password", Password ?? Environment.GetEnvironmentVariable("ZGWK_TWINPACK_PWD"));
            request.Headers.Add("zgwk-token", UserInfo?.Token);
            request.Headers.Add("twinpack-client-version", ClientVersion.ToString());
        }

        public async Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion, CancellationToken cancellationToken = default)
        {
            _logger.Info($"Uploading Package '{packageVersion.Name}' (branch: {packageVersion.Branch}, target: {packageVersion.Target}, configuration: {packageVersion.Configuration}, version: {packageVersion.Version})");

            var requestBodyJson = JsonSerializer.Serialize(packageVersion);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/package-version"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);

            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch (JsonException ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new PostException("Response could not be parsed");
            }
            
            if(result.Meta?.Message != null)
                throw new PostException(result.Meta.Message.ToString());

            if(result.PackageVersionId == null)
                throw new PostException("Error occured while pushing to the Twinpack server");

            return result;
        }

        public async Task<Tuple<IEnumerable<T>, bool>> QueryWithPaginationAsync<T>(string endpoint, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            var results = new List<T>();
            var uri = new Uri(TwinpackUrl + $"/{endpoint}?page={page}&per_page={perPage}");
            PaginationHeader pagination = null;

            var hasNextPage = true;
            while (hasNextPage)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
                AddHeaders(request);

                var response = await _client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadAsStringAsync();            

                try
                {
                    results.AddRange(JsonSerializer.Deserialize<List<T>>(data));
                }
                catch(JsonException ex)
                {
                    _logger.Trace($"Unparseable response: {data}");
                    throw new GetException("Response could not be parsed");
                }

                var linkHeader = response.Headers.GetValues("Link");
                if (linkHeader.Any())
                {
                    var h = Regex.Unescape(linkHeader.First());

                    try
                    {
                        pagination = JsonSerializer.Deserialize<PaginationHeader>(h);
                    }
                    catch(JsonException ex)
                    {
                        _logger.Trace($"Unparseable response: {linkHeader.First()}");
                        throw new GetException("Response could not be parsed");
                    }
                    
                    if (pagination.Next == null)
                    {
                         hasNextPage = false;
                    }

                    if(pagination?.Next != null)
                        uri = new Uri(pagination.Next);
                }

                if (results.Count() >= perPage)
                    return new Tuple<IEnumerable<T>, bool>(results.Take(perPage), pagination.Next != null);
            }

            return new Tuple<IEnumerable<T>, bool>(results.Take(perPage), pagination.Next != null);
        }

        public async Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            return await QueryWithPaginationAsync<CatalogItemGetResponse>($"catalog" +
                                                $"?search={HttpUtility.UrlEncode(search)}", page, perPage, cancellationToken);
        }

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(int packageId, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            return await QueryWithPaginationAsync<PackageVersionGetResponse>($"package-versions" +
                                            $"?id={packageId}", page, perPage, cancellationToken);
        }

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(int packageId, string branch, string configuration, string target, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            return await QueryWithPaginationAsync<PackageVersionGetResponse>($"package-versions" +
                                            $"?id={packageId}" +
                                            $"&branch={HttpUtility.UrlEncode(branch)}" +
                                            $"&target={HttpUtility.UrlEncode(target)}" +
                                            $"&configuration={HttpUtility.UrlEncode(configuration)}", page, perPage, cancellationToken);
        }

        public async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget=null, string preferredConfiguration=null, string preferredBranch=null, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package-resolve" +
                $"?distributor-name={HttpUtility.UrlEncode(library.DistributorName)}" +
                $"&name={HttpUtility.UrlEncode(library.Name)}" +
                $"&version={HttpUtility.UrlEncode(library.Version)}" +
                $"&target={HttpUtility.UrlEncode(preferredTarget)}" +
                $"&configuration={HttpUtility.UrlEncode(preferredConfiguration)}" +
                $"&branch={HttpUtility.UrlEncode(preferredBranch)}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
            AddHeaders(request);

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch(JsonException ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new GetException("Response could not be parsed");
            }

            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        private string Checksum(string fp)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (var fileStream = File.OpenRead(fp))
            {
                byte[] hashBytes = sha256.ComputeHash(fileStream);
                return BitConverter.ToString(hashBytes).Replace("-", "");
            }
        }

        private async Task<bool> DownloadPackageVersionFromDownloadUrlAsync(PackageVersionGetResponse packageVersion, string fileName, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info($"Downloading {packageVersion.Title} (version: {packageVersion.Version}, distributor: {packageVersion.DistributorName}) (from {packageVersion.BinaryDownloadUrl})");

                var responseDownload = await _client.GetAsync(packageVersion.BinaryDownloadUrl, cancellationToken);
                responseDownload.EnsureSuccessStatusCode();

                using (var fileStream = File.Create(fileName))
                {
                    var binaryStream = await responseDownload.Content.ReadAsStreamAsync();
                    await binaryStream.CopyToAsync(fileStream);
                }

                var chk = Checksum(fileName);
                if (!string.IsNullOrEmpty(packageVersion.BinarySha256) && !string.Equals(chk, packageVersion.BinarySha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (ChecksumMode.IgnoreMismatch == checksumMode)
                    {
                        _logger.Warn("Checksum mismatch is ignored");
                        return true;
                    }
                    else
                    {
                        throw new ChecksumException($"Checksum of downloaded file is mismatching, library was changed after its release!", packageVersion.BinarySha256, chk);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ChecksumException)
            {
                return false;
            }
            catch
            {
                _logger.Warn($"Download failed {packageVersion.BinaryDownloadUrl}");
                return false;
            }

            return true;
        }

        public async Task DownloadPackageVersionAsync(PackageVersionGetResponse packageVersion, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default)
        {
            var extension = packageVersion.Compiled == 1 ? "compiled-library" : "library";
            var filePath = $@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}";
            var fileName = $@"{filePath}\{packageVersion.Name}_{packageVersion.Version}.{extension}";
            Directory.CreateDirectory(filePath);

            // first try to download from a url
            if (!string.IsNullOrEmpty(packageVersion.BinaryDownloadUrl))
            {
                try
                {
                    var downloadOk = await DownloadPackageVersionFromDownloadUrlAsync(packageVersion, fileName, checksumMode, cachePath, cancellationToken);
                    if(downloadOk)
                        return;
                }
                catch (ChecksumException)
                {
                    if (ChecksumMode.IgnoreMismatchAndFallack != checksumMode)
                        throw;
                }
            }


            // if this doesn't succeed download from Twinpack
            _logger.Info($"Downloading {packageVersion.Title} (version: {packageVersion.Version}, distributor: {packageVersion.DistributorName}) (from {TwinpackUrlBase})");

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package-version" +
                $"?id={packageVersion.PackageVersionId}&include-binary=1"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
            AddHeaders(request);

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new GetException("Response could not be parsed");
            }

            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            if (result.PackageId != null)
            {
                File.WriteAllBytes(fileName, Convert.FromBase64String(result.Binary));

                var chk = Checksum(fileName);
                if (!string.IsNullOrEmpty(packageVersion.BinarySha256) && !string.Equals(chk, packageVersion.BinarySha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (ChecksumMode.IgnoreMismatch == checksumMode)
                    {
                        _logger.Warn("Checksum mismatch is ignored");
                        return;
                    }
                    else
                    {
                        throw new ChecksumException($"Checksum of downloaded file is mismatching, library was changed after its release!", packageVersion.BinarySha256, chk);
                    }
                }
            }
        }

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(int packageVersionId, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package-version" +
                $"?id={packageVersionId}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
            AddHeaders(request);

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch(JsonException ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new GetException("Response could not be parsed");
            }
            
            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());
            
            return result;
        }

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(string distributorName, string name, string version, string configuration="Release", string branch="main", string target="TC3.1", CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package-version" +
                $"?distributor-name={HttpUtility.UrlEncode(distributorName)}" +
                $"&name={HttpUtility.UrlEncode(name)}" +
                $"&version={HttpUtility.UrlEncode(version)}" +
                $"&configuration={HttpUtility.UrlEncode(configuration)}" +
                $"&branch={HttpUtility.UrlEncode(branch)}" +
                $"&target={HttpUtility.UrlEncode(target)}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch(Exception ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new GetException($"Response could not be parsed\n{responseBody}");
            }
            
            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package" +
                $"?distributor-name={HttpUtility.UrlEncode(distributorName)}" +
                $"&name={HttpUtility.UrlEncode(packageName)}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
            }
            catch(JsonException ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new GetException("Response could not be parsed");
            }
            
            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageGetResponse> GetPackageAsync(int packageId, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package?id={packageId}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
            }
            catch(JsonException ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new GetException("Response could not be parsed");
            }
            
            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageVersionGetResponse> PutPackageVersionAsync(PackageVersionPatchRequest package, CancellationToken cancellationToken = default)
        {
            _logger.Info("Updating package version");
            var requestBodyJson = JsonSerializer.Serialize(package);

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/package-version"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
            AddHeaders(request);
            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch(JsonException ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new PutException("Response could not be parsed");
            }
            
            if (result.Meta?.Message != null)
                throw new PutException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageGetResponse> PutPackageAsync(PackagePatchRequest package, CancellationToken cancellationToken = default)
        {
            _logger.Info("Updating general package");
            var requestBodyJson = JsonSerializer.Serialize(package);

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/package"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
            }
            catch(JsonException ex)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new PutException("Response could not be parsed");
            }
            
            if (result.Meta?.Message != null)
                throw new PutException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<LoginPostResponse> LoginAsync(string username = null, string password = null, CancellationToken cancellationToken = default)
        {
            _client.Invalidate(); // clear the cache
            var credentials = CredentialManager.GetCredentials(TwinpackUrlBase);

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/login"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            Username = username ?? credentials?.UserName;
            Password = password ?? credentials?.Password;
            AddHeaders(request);
            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            try
            {
                LoginPostResponse result = null;
                try
                {
                    result = JsonSerializer.Deserialize<LoginPostResponse>(responseBody);
                }
                catch(JsonException ex)
                {
                    _logger.Trace($"Unparseable response: {responseBody}");
                    throw new LoginException("Response could not be parsed");
                }
                
                if (result.Meta?.Message != null)
                    throw new LoginException(result.Meta.Message.ToString());

                UserInfo = result;

                if(!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                    CredentialManager.SaveCredentials(TwinpackUrlBase, new System.Net.NetworkCredential(Username, Password));

                if(IsClientUpdateAvailable)
                    _logger.Info($"Twinpack {UserInfo?.UpdateVersion} is available! Download and install the lastest version at {UserInfo.UpdateUrl}");

                _logger.Info("Log in to Twinpack Server successful");
                return UserInfo;
            }
            catch
            {
                DeleteCredential();
                throw;
            }
        }

        public async Task PushAsync(IEnumerable<ConfigPlcProject> plcs, string configuration, string branch, string target, string notes, bool compiled, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
            foreach (var plc in plcs)
            {
                try
                {
                    string binary = Convert.ToBase64String(File.ReadAllBytes(plc.FilePath));
                    string licenseBinary = (!File.Exists(plc.LicenseFile) || string.IsNullOrEmpty(plc.LicenseFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.LicenseFile));
                    string licenseTmcBinary = (!File.Exists(plc.LicenseTmcFile) || string.IsNullOrEmpty(plc.LicenseTmcFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.LicenseTmcFile));
                    string iconBinary = (!File.Exists(plc.IconFile) || string.IsNullOrEmpty(plc.IconFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.IconFile));

                    var packageVersion = new PackageVersionPostRequest()
                    {
                        Name = plc.Name,
                        Version = plc.Version,
                        Target = target,
                        License = plc.License,
                        Description = plc.Description,
                        DistributorName = plc.DistributorName,
                        Authors = plc.Authors,
                        Entitlement = plc.Entitlement,
                        ProjectUrl = plc.ProjectUrl,
                        DisplayName = plc.DisplayName,
                        Branch = branch,
                        Configuration = configuration,
                        Compiled = compiled ? 1 : 0,
                        Notes = notes,
                        IconFilename = Path.GetFileName(plc.IconFile),
                        IconBinary = iconBinary,
                        BinaryDownloadUrl = plc.BinaryDownloadUrl,
                        LicenseBinary = licenseBinary,
                        LicenseTmcBinary = licenseTmcBinary,
                        Binary = binary,
                        Dependencies = plc.Packages?.Select(x => new PackageVersionDependencyPostRequest
                        {
                            Repository = x.Repository,
                            DistributorName = x.DistributorName,
                            Name = x.Name,
                            Version = x.Version,
                            Branch = x.Branch,
                            Target = x.Target,
                            Configuration = x.Configuration
                        })
                    };

                    await PostPackageVersionAsync(packageVersion, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"{plc.Name}: {ex.Message}");
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Any())
            {
                throw new PostException($"Pushing to Twinpack Server failed for {exceptions.Count()} packages!");
            }
        }

        public async Task PullAsync(bool skipInternalPackages = false, string rootPath = ".", string cachePath = null, CancellationToken cancellationToken = default)
        {
            var config = ConfigFactory.Load(path: rootPath);

            _logger.Info($"Pulling from Twinpack Server (Skipping internal packages: {skipInternalPackages})");
            var plcs = config.Projects.SelectMany(x => x.Plcs);
            var exceptions = new List<Exception>();
            var handled = new List<ConfigPlcPackage>();
            foreach (var plc in plcs)
            {
                // skip packages that are provided according to the config file
                if (skipInternalPackages)
                {
                    _logger.Info($"Package {plc.Name} {plc.Version} is provided");
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

                    try
                    {
                        var packageVersion = await GetPackageVersionAsync(package.DistributorName, package.Name, package.Version, package.Configuration, package.Branch, package.Target);
                        handled.Add(package);

                        if (packageVersion?.PackageVersionId == null)
                            throw new GetException("Package not available");

                        await DownloadPackageVersionAsync(packageVersion, checksumMode: ChecksumMode.IgnoreMismatch, cachePath: cachePath, cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"{package.Name}: {ex.Message}");
                        exceptions.Add(ex);
                    }
                }
            }

            if (exceptions.Any())
            {
                throw new Exceptions.GetException($"Pulling for Twinpack Server failed for {exceptions.Count()} dependencies!");
            }
        }

        private void DeleteCredential()
        {
            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";
            try
            {
                CredentialManager.RemoveCredentials(TwinpackUrlBase);
            }
            catch { }
        }

        public void Logout()
        {
            _logger.Info("Log out from Twinpack Server");

            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";

            try
            {
                CredentialManager.RemoveCredentials(TwinpackUrlBase);
            }
            catch (Exception) { }
        }

        public void InvalidateCache()
        {
            _logger.Info("Resetting Twinpack Cache");
            _client.Invalidate();
        }
    }
}
