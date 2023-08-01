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

        public string TwinpackUrl = "https://twinpack.dev";
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

        public void AddHeaders(System.Net.Http.HttpRequestMessage request)
        {
            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);
            request.Headers.Add("twinpack-client-version", ClientVersion.ToString());
        }

        public async Task PullAsync(string configuration, string branch, string target, string cachePath = null)
        {
            var config = ConfigFactory.Load();

            _logger.Info($"Pulling from Twinpack Server");
            var plcs = config.Projects.SelectMany(x => x.Plcs);

            var exceptions = new List<Exception>();
            foreach (var plc in plcs)
            {
                foreach (var package in plc.Packages ?? new List<ConfigPlcPackage>())
                {
                    try
                    {
                        await GetPackageVersionAsync(package.DistributorName, package.Name, package.Version, configuration, branch, target, true, cachePath: cachePath);
                    }
                    catch(Exception ex)
                    {
                        _logger.Warn($"{package.Name}: {ex.Message}");
                        exceptions.Add(ex);
                    }
                }
            }

            if(exceptions.Any())
            {
                throw new GetException($"Pulling for Twinpack Server failed for {exceptions.Count()} dependencies!");
            }         
        }

        public async Task PushAsync(string configuration, string branch, string target, string notes, bool compiled, string cachePath = null)
        {
            var config = ConfigFactory.Load();

            _logger.Info($"Pushing to Twinpack Server");

            var suffix = compiled ? "compiled-library" : "library";
            var plcs = config.Projects.SelectMany(x => x.Plcs)
                                         .Where(x => x.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary ||
                                                x.PlcType == ConfigPlcProject.PlcProjectType.Library);
            // check if all requested files are present
            foreach (var plc in plcs)
            {
                var fileName = $@"{cachePath ?? DefaultLibraryCachePath}\{target}\{plc.Name}_{plc.Version}.{suffix}";
                if (!File.Exists(fileName))
                    throw new LibraryNotFoundException(plc.Name, plc.Version, $"Could not find library file '{fileName}'");

                if (!string.IsNullOrEmpty(plc.LicenseFile) && !File.Exists(plc.LicenseFile))
                {
                    _logger.Warn($"Could not find license file '{plc.LicenseFile}'");
                    //    throw new LibraryNotFoundException(plc.Name, plc.Version, $"Could not find license file '{plc.LicenseFile}'");
                }
            }

            var exceptions = new List<Exception>();
            foreach (var plc in plcs)
            {
                try
                {
                    await PostPackageVersionAsync(plc, configuration, branch, target, notes, compiled);
                }
                catch(Exception ex)
                {
                    _logger.Warn($"{plc.Name}: {ex.Message}");
                    exceptions.Add(ex);
                }
            }

            if(exceptions.Any())
            {
                throw new PostException($"Pushing to Twinpack Server failed for {exceptions.Count()} packages!");
            }
        }

        public async Task<PackageVersionGetResponse> PostPackageVersionAsync(ConfigPlcProject plc, string configuration, string branch, string target, string notes, bool compiled, string cachePath = null)
        {
            _logger.Info($"Uploading Package '{plc.Name}' (branch: {branch}, target: {target}, configuration: {configuration}, version: {plc.Version}");

            var suffix = compiled ? "compiled-library" : "library";
            string binary = Convert.ToBase64String(File.ReadAllBytes($@"{cachePath ?? DefaultLibraryCachePath}\{target}\{plc.Name}_{plc.Version}.{suffix}"));
            string licenseBinary = (!File.Exists(plc.LicenseFile) || string.IsNullOrEmpty(plc.LicenseFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.LicenseFile));
            string iconBinary = (!File.Exists(plc.IconFile) || string.IsNullOrEmpty(plc.IconFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.IconFile));

            var requestBody = new PackageVersionPostRequest()
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
                LicenseBinary = licenseBinary,
                Binary = binary,
                Dependencies = plc.Packages?.Select(x => new PackageVersionDependencyPostRequest {
                    Repository = x.Repository,
                    DistributorName = x.DistributorName,
                    Name = x.Name,
                    Version = x.Version,
                    Branch = x.Branch,
                    Target = x.Target,
                    Configuration = x.Configuration
                })
            };      

            var requestBodyJson = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/twinpack.php?controller=package-version"));
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

        public async Task<IEnumerable<T>> QueryWithPagination<T>(string endpoint, int page = 1, int perPage = 5)
        {
            var results = new List<T>();
            var query = $"/{endpoint}";
            var uri = new Uri(TwinpackUrl + query + $"&page={page}&per_page={perPage}");

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

                if (results.Count() >= perPage)
                    return results.Take(perPage);

                var linkHeader = response.Headers.GetValues("Link");
                if (linkHeader.Any())
                {
                    var h = Regex.Unescape(linkHeader.First());
                    PaginationHeader pagination = null;

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
                         break;
                    }

                    uri = new Uri(pagination.Next);
                }
            }

            return results;
        }

        public async Task<IEnumerable<CatalogItemGetResponse>> GetCatalogAsync(string search, int page = 1, int perPage = 5)
        {
            return await QueryWithPagination<CatalogItemGetResponse>($"twinpack.php?controller=catalog" +
                                                $"&search={search}", page, perPage);
        }

        public async Task<IEnumerable<PackageVersionsItemGetResponse>> GetPackageVersionsAsync(int packageId, int page = 1, int perPage = 5)
        {
            return await QueryWithPagination<PackageVersionsItemGetResponse>($"twinpack.php?controller=package-versions" +
                                            $"&id={packageId}", page, perPage);
        }

        public async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package-resolve" +
                $"&distributor-name={library.DistributorName}&name={library.Name}&version={library.Version}"));

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
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package-version" +
                $"&id={packageVersionId}&include-binary={(includeBinary ? 1 : 0)}"));

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

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package-version" +
                $"&distributor-name={HttpUtility.UrlEncode(distributorName)}" +
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

            var filePath = $@"{cachePath ?? DefaultLibraryCachePath}\{target}";
                
            if (includeBinary)
            {
                var extension = result.Compiled == 1 ? "compiled-library" : "library";
                File.WriteAllText($@"{filePath}\{result.Name}_{result.Version}.{extension}", Encoding.ASCII.GetString(Convert.FromBase64String(result.Binary)));
            }

            return result;
        }

        public async Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package" +
                $"&distributor-name={HttpUtility.UrlEncode(distributorName)}" +
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
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package&id={packageId}"));

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

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/twinpack.php?controller=package-version"));
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

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/twinpack.php?controller=package"));
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
            var credentials = CredentialManager.ReadCredential(TwinpackUrl);

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/twinpack.php?controller=login"));
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
                    CredentialManager.WriteCredential(TwinpackUrl, Username, Password, CredentialPersistence.LocalMachine);

                if(UserInfo?.UpdateVersion != null && new Version(UserInfo?.UpdateVersion) > ClientVersion)
                    _logger.Info($"Twinpack {UserInfo?.UpdateVersion} is available! Download and install the lastest version at {UserInfo.UpdateUrl}");

                _logger.Info("Log in to Twinpack Server successful");
                return UserInfo;
            }
            catch (Exception ex)
            {
                _logger.Info("Log in to Twinpack Server failed");
                _logger.Error(ex.Message);
                UserInfo = new LoginPostResponse();
                Username = "";
                Password = "";
                CredentialManager.DeleteCredential(TwinpackUrl);
                throw ex;
            }
        }

        public void Logout()
        {
            _logger.Info("Log out from Twinpack Server");

            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";

            try
            {
                CredentialManager.DeleteCredential(TwinpackUrl);
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
