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
using Twinpack.Models.Api;
using Twinpack.Exceptions;
using System.Reflection;
using AdysTech.CredentialManager;
using System.Threading;
using System.Security.Cryptography;
using System.Runtime.Remoting.Messaging;
using System.Runtime.CompilerServices;

namespace Twinpack.Protocol
{
    class NativePackagingServerFactory : IPackagingServerFactory
    {
        public IPackageServer Create(string name, string uri)
        {
            return new TwinpackServer(name, uri);
        }

        public string ServerType { get; } = "Twinpack Repository";
    }
    public class TwinpackServer : IPackageServer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }
        public const string DefaultUrlBase = "https://twinpack.dev";

        private CachedHttpClient _client = new CachedHttpClient();

        public string ServerType { get; } = "Twinpack Repository";
        public string Name { get; set; }
        public string UrlBase { get; set; }
        public string Url
        {
            get => UrlBase + "/index.php";
        }
        public string UrlRegister
        {
            get => UrlBase + "/wp-login.php";
        }

        public string Username { get; set; }
        public string Password { get; set; }
        public LoginPostResponse UserInfo { get; set; }
        public bool LoggedIn { get { return UserInfo?.User != null; } }
        public bool Connected { get { return UserInfo?.UpdateVersion != null; } }

        public TwinpackServer(string name = "twinpack.dev", string url = null)
        {
            Name = name;
            UrlBase = url ?? DefaultUrlBase;
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

        private void AddHeaders(System.Net.Http.HttpRequestMessage request)
        {
            request.Headers.Add("zgwk-username", Username ?? Environment.GetEnvironmentVariable("ZGWK_TWINPACK_USER"));
            request.Headers.Add("zgwk-password", Password ?? Environment.GetEnvironmentVariable("ZGWK_TWINPACK_PWD"));
            request.Headers.Add("zgwk-token", UserInfo?.Token);
            request.Headers.Add("twinpack-client-version", ClientVersion.ToString());
        }

        public async Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion, CancellationToken cancellationToken = default)
        {
            _logger.Info($"Uploading {packageVersion.Name} {packageVersion.Version} (branch: {packageVersion.Branch}, target: {packageVersion.Target}, configuration: {packageVersion.Configuration})");

            var requestBodyJson = JsonSerializer.Serialize(packageVersion);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Url + "/package-version"));
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
            catch (JsonException)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new PostException("Response could not be parsed");
            }

            if (result.Meta?.Message != null)
                throw new PostException(result.Meta.Message.ToString());

            if (result.PackageVersionId == null)
                throw new PostException("Error occured while pushing to the Twinpack server");

            return result;
        }

        private async Task<Tuple<IEnumerable<T>, bool>> QueryWithPaginationAsync<T>(string endpoint, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            var results = new List<T>();
            var uri = new Uri(Url + $"/{endpoint}?page={page}&per_page={perPage}");
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
                catch (JsonException)
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
                    catch (JsonException)
                    {
                        _logger.Trace($"Unparseable response: {linkHeader.First()}");
                        throw new GetException("Response could not be parsed");
                    }

                    if (pagination.Next == null)
                    {
                        hasNextPage = false;
                    }

                    if (pagination?.Next != null)
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

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, string branch=null, string configuration=null, string target=null, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            return await QueryWithPaginationAsync<PackageVersionGetResponse>($"package-versions" +
                                            $"?distributor-name={HttpUtility.UrlEncode(library.DistributorName)}" +
                                            $"&name={HttpUtility.UrlEncode(library.Name)}" +
                                            $"&version={HttpUtility.UrlEncode(library.Version)}" +
                                            (branch != null ? $"&branch={HttpUtility.UrlEncode(branch)}" : "") +
                                            (target != null ? $"&target={HttpUtility.UrlEncode(target)}" : "") +
                                            (configuration != null ? $"&configuration={HttpUtility.UrlEncode(configuration)}" : ""),
                                            page, perPage, cancellationToken);
        }

        public async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Url + $"/package-resolve" +
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
            catch (JsonException)
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

            _logger.Info($"Downloaded {packageVersion.Title} {packageVersion.Version} (distributor: {packageVersion.DistributorName}) (from {packageVersion.BinaryDownloadUrl})");
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
                    if (downloadOk)
                        return;
                }
                catch (ChecksumException)
                {
                    if (ChecksumMode.IgnoreMismatchAndFallack != checksumMode)
                        throw;
                }
            }

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Url + $"/package-version" +
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


                // if this doesn't succeed download from Twinpack
                _logger.Info($"Downloaded {packageVersion.Title} {packageVersion.Version} (distributor: {packageVersion.DistributorName}) (from {UrlBase})");

            }
        }

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(PlcLibrary library, string branch, string configuration, string target, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Url + $"/package-version" +
                                            $"?distributor-name={library.DistributorName}" +
                                            $"&name={library.Name}" +
                                            $"&version={library.Version}" +
                                            $"&branch={HttpUtility.UrlEncode(branch)}" +
                                            $"&target={HttpUtility.UrlEncode(target)}" +
                                            $"&configuration={HttpUtility.UrlEncode(configuration)}"
                                            ));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
            AddHeaders(request);

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch (JsonException)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new GetException("Response could not be parsed");
            }

            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageVersionGetResponse> PutPackageVersionDownloadsAsync(PackageVersionDownloadsPutRequest packageVersionDownloads, CancellationToken cancellationToken = default)
        {
            var requestBodyJson = JsonSerializer.Serialize(packageVersionDownloads);
            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Url + $"/package-version/downloads"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);

            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch (JsonException)
            {
                _logger.Trace($"Unparseable response: {responseBody}");
                throw new PutException("Response could not be parsed");
            }

            if (result.Meta?.Message != null)
                throw new PutException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(Url + $"/package" +
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
            catch (JsonException)
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

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Url + "/package-version"));
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
            catch (JsonException)
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

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(Url + "/package"));
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
            catch (JsonException)
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
            var credentials = CredentialManager.GetCredentials(UrlBase);

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Url + "/login"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            Username = username ?? credentials?.UserName;
            Password = password ?? credentials?.Password;

            // reset token to get a new one
            if (UserInfo?.Token != null)
                UserInfo.Token = null;

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
                catch (JsonException)
                {
                    _logger.Trace($"Unparseable response: {responseBody}");
                    throw new LoginException("Response could not be parsed");
                }

                if (result.Meta?.Message != null)
                    throw new LoginException(result.Meta.Message.ToString());

                UserInfo = result;

                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                    CredentialManager.SaveCredentials(UrlBase, new System.Net.NetworkCredential(Username, Password));

                if (IsClientUpdateAvailable)
                    _logger.Info($"Twinpack {UserInfo?.UpdateVersion} is available! Download and install the lastest version at {UserInfo.UpdateUrl}");

                //_ = RefreshTokenAsync();

                _logger.Info("Log in to Twinpack Server successful");
                return UserInfo;
            }
            catch
            {
                DeleteCredential();
                throw;
            }
        }

        private async Task RefreshTokenAsync()
        {
            if (UserInfo?.Token == null)
                return;

            var tokenParts = UserInfo.Token.Split('.');
            if (tokenParts.Length == 3)
            {
                var payload = JsonSerializer.Deserialize<JwtPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(tokenParts[1])));
                var delay = new TimeSpan(0, 0, payload.ExpirationTime).Subtract(DateTime.UtcNow - new DateTime(1970, 1, 1));

                if (UserInfo?.Token != null)
                {
                    await Task.Delay(delay, CancellationToken.None);
                    await LoginAsync();
                }

            }
        }

        public async Task PushAsync(IEnumerable<ConfigPlcProject> plcs, string configuration, string branch, string target, string notes, bool compiled, bool skipDuplicate = false, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
            foreach (var plc in plcs)
            {
                try
                {
                    // check if package version already exists and skip it
                    var packageVersionLookup = await GetPackageVersionAsync(new PlcLibrary { DistributorName = plc.DistributorName, Name = plc.Name, Version = plc.Version }, branch, configuration, target, cancellationToken);
                    if (packageVersionLookup.PackageVersionId != null)
                    {
                        string msg = $"already published package '{packageVersionLookup.Name}' (branch: {packageVersionLookup.Branch}, target: {packageVersionLookup.Target}, configuration: {packageVersionLookup.Configuration}, version: {packageVersionLookup.Version})";
                        if (skipDuplicate)
                        {
                            _logger.Info($"Skipping " + msg);
                            continue;
                        }
                        else
                        {
                            _logger.Warn($"Uploading " + msg);
                        }
                    }

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
                        Dependencies = plc.Packages?.Select(x => new PackageVersionDependency
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task LogoutAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            _logger.Trace("Log out from Twinpack Server");

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
            _logger.Info("Resetting Twinpack Cache");
            _client.Invalidate();
        }
    }
}
