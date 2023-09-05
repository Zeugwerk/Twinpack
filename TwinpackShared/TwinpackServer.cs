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
using Meziantou.Framework.Win32;
using System.Reflection;

namespace Twinpack
{
    public class TwinpackServer
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        private CachedHttpClient _client = new CachedHttpClient();

        public string TwinpackUrlBase = "https://twinpack.dev";
        public string TwinpackUrl = "https://twinpack.dev/index.php";
        public string RegisterUrl = "https://twinpack.dev/wp-login.php";

        public string Username { get; set; }
        public string Password { get; set; }
        public LoginPostResponse UserInfo { get; set; }
        public bool LoggedIn { get { return UserInfo?.User != null; } }

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
            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);
            request.Headers.Add("twinpack-client-version", ClientVersion.ToString());
        }

        public async Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion)
        {
            _logger.Info($"Uploading Package '{packageVersion.Name}' (branch: {packageVersion.Branch}, target: {packageVersion.Target}, configuration: {packageVersion.Configuration}, version: {packageVersion.Version}");

            var requestBodyJson = JsonSerializer.Serialize(packageVersion);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/package-version"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);

            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            PackageVersionGetResponse result = null;
            try
            {
                result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            }
            catch(JsonException ex)
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

        public async Task<Tuple<IEnumerable<T>, bool>> QueryWithPagination<T>(string endpoint, int page = 1, int perPage = 5)
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

                var response = await _client.SendAsync(request);
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

        public async Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5)
        {
            return await QueryWithPagination<CatalogItemGetResponse>($"catalog" +
                                                $"?search={HttpUtility.UrlEncode(search)}", page, perPage);
        }

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(int packageId, int page = 1, int perPage = 5)
        {
            return await QueryWithPagination<PackageVersionGetResponse>($"package-versions" +
                                            $"?id={packageId}", page, perPage);
        }

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(int packageId, string branch, string configuration, string target, int page = 1, int perPage = 5)
        {
            return await QueryWithPagination<PackageVersionGetResponse>($"package-versions" +
                                            $"?id={packageId}" +
                                            $"&branch={HttpUtility.UrlEncode(branch)}" +
                                            $"&target={HttpUtility.UrlEncode(target)}" +
                                            $"&configuration={HttpUtility.UrlEncode(configuration)}", page, perPage);
        }

        public async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget=null, string preferredConfiguration=null, string preferredBranch=null)
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

            var response = await _client.SendAsync(request);
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

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(int packageVersionId, bool includeBinary, string cachePath = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package-version" +
                $"?id={packageVersionId}&include-binary={(includeBinary ? 1 : 0)}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
            AddHeaders(request);

            var response = await _client.SendAsync(request);
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

            if (includeBinary)
            {
                var filePath = $@"{cachePath ?? DefaultLibraryCachePath}\{result.Target}";
                Directory.CreateDirectory(filePath);
                var extension = result.Compiled == 1 ? "compiled-library" : "library";
                File.WriteAllBytes($@"{filePath}\{result.Name}_{result.Version}.{extension}", Convert.FromBase64String(result.Binary));
            }
            
            return result;
        }

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(string distributorName, string name, string version, string configuration, string branch, string target, bool includeBinary = false, string cachePath = null)
        {
            if(includeBinary)
                _logger.Info($"Downloading Package '{name}' (version: {version}, configuration: {configuration}, branch: {branch}, target: {target})");

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package-version" +
                $"?distributor-name={HttpUtility.UrlEncode(distributorName)}" +
                $"&name={HttpUtility.UrlEncode(name)}" +
                $"&version={HttpUtility.UrlEncode(version)}" +
                $"&configuration={HttpUtility.UrlEncode(configuration)}" +
                $"&branch={HttpUtility.UrlEncode(branch)}" +
                $"&target={HttpUtility.UrlEncode(target)}" +
                $"&include-binary={(includeBinary ? 1 : 0)}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            var response = await _client.SendAsync(request);
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
               
            if (includeBinary)
            {
                var filePath = $@"{cachePath ?? DefaultLibraryCachePath}\{result.Target}";
                Directory.CreateDirectory(filePath);                
                var extension = result.Compiled == 1 ? "compiled-library" : "library";
                File.WriteAllBytes($@"{filePath}\{result.Name}_{result.Version}.{extension}", Convert.FromBase64String(result.Binary));
            }

            return result;
        }

        public async Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package" +
                $"?distributor-name={HttpUtility.UrlEncode(distributorName)}" +
                $"&name={HttpUtility.UrlEncode(packageName)}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            var response = await _client.SendAsync(request);
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

        public async Task<PackageGetResponse> GetPackageAsync(int packageId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/package?id={packageId}"));

            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            var response = await _client.SendAsync(request);
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

        public async Task<PackageVersionGetResponse> PutPackageVersionAsync(PackageVersionPatchRequest package)
        {
            _logger.Info("Updating package version");
            var requestBodyJson = JsonSerializer.Serialize(package);

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/package-version"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");
            AddHeaders(request);
            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request);
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

        public async Task<PackageGetResponse> PutPackageAsync(PackagePatchRequest package)
        {
            _logger.Info("Updating general package");
            var requestBodyJson = JsonSerializer.Serialize(package);

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/package"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            AddHeaders(request);
            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request);
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

        public async Task<LoginPostResponse> LoginAsync(string username = null, string password = null)
        {
            var credentials = CredentialManager.ReadCredential(TwinpackUrlBase);

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/login"));
            _logger.Trace($"{request.Method.Method}: {request.RequestUri}");

            Username = username ?? credentials?.UserName;
            Password = password ?? credentials?.Password;
            AddHeaders(request);
            var response = await _client.SendAsync(request);
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
                    CredentialManager.WriteCredential(TwinpackUrlBase, Username, Password, CredentialPersistence.LocalMachine);

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

        private void DeleteCredential()
        {
            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";
            try
            {
                CredentialManager.DeleteCredential(TwinpackUrlBase);
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
                CredentialManager.DeleteCredential(TwinpackUrlBase);
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
