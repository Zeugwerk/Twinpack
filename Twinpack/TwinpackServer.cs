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

namespace Twinpack
{
    public class TwinpackServer
    {
        public static string TwinpackUrl = "https://zeugwerk.dev";
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }
        public static string DefaultUsername = "public";
        public static string DefaultPassword = "public";
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private CachedHttpClient _client = new CachedHttpClient();

        public string Username { get; set; }
        public string Password { get; set; }
        public LoginPostResponse UserInfo { get; set; }
        public bool LoggedIn { get { return UserInfo?.User != null; } }

        public async Task PullAsync(string configuration, string branch, string target, string cachePath = null)
        {
            var config = ConfigFactory.Load();

            _logger.Info($"Pulling packages required by {config.Solution} from Twinpack Server");
            var plcs = config.Projects.SelectMany(x => x.Plcs);

            foreach (var plc in plcs)
            {
                foreach (var package in plc.Packages ?? new List<ConfigPlcPackage>())
                {
                    _logger.Info($"Downloading {package.Name} (version: {package.Version}, configuration: {configuration}, branch: {package.Branch}, target: {target})");
                    await GetPackageVersionAsync(package.Repository, package.Name, package.Version, configuration, branch, target, true, cachePath: cachePath);
                }
            }
        }

        public async Task PushAsync(string configuration, string branch, string target, string notes, bool compiled, string cachePath = null)
        {
            var config = ConfigFactory.Load();

            _logger.Info($"Pushing packages of {config.Solution} to Twinpack Server");
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

            foreach (var plc in plcs)
            {
                await PostPackageVersionAsync(plc, configuration, branch, target, notes, compiled);
            }
        }

        public async Task<PackageVersionGetResponse> PostPackageVersionAsync(ConfigPlcProject plc, string configuration, string branch, string target, string notes, bool compiled, string cachePath = null)
        {
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
            _logger.Debug($"Pushing {requestBody.Name} (branch: {requestBody.Branch}, target: {requestBody.Target}, configuration: {requestBody.Configuration}, version: {requestBody.Version}, dependencies: {requestBody.Dependencies.Count()})");

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/twinpack.php?controller=package-version"));
            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);
            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            
            var result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
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
                request.Headers.Add("zgwk-username", Username);
                request.Headers.Add("zgwk-password", Password);

                var response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadAsStringAsync();
                results.AddRange(JsonSerializer.Deserialize<List<T>>(data));

                if (results.Count() >= perPage)
                    return results.Take(perPage);

                var linkHeader = response.Headers.GetValues("Link");
                if (linkHeader.Any())
                {
                    var h = Regex.Unescape(linkHeader.First());

                    var pagination = JsonSerializer.Deserialize<PaginationHeader>(h);
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
            _logger.Info($"Retrieving package catalog of Twinpack Server");
            return await QueryWithPagination<CatalogItemGetResponse>($"twinpack.php?controller=catalog" +
                                                $"&search={search}", page, perPage);
        }

        public async Task<IEnumerable<PackageVersionsItemGetResponse>> GetPackageVersionsAsync(int packageId, int page = 1, int perPage = 5)
        {
            _logger.Info($"Retrieving package catalog of Twinpack Server");
            return await QueryWithPagination<PackageVersionsItemGetResponse>($"twinpack.php?controller=package-versions" +
                                            $"&id={packageId}", page, perPage);
        }

        public async Task<IEnumerable<PackageVersionsItemGetResponse>> GetPackageVersionsAsync(string repository, string name, int page = 1, int perPage = 5)
        {
            _logger.Info($"Retrieving package catalog of Twinpack Server");
            return await QueryWithPagination<PackageVersionsItemGetResponse>($"twinpack.php?controller=package-versions" +
                                                        $"&repository={HttpUtility.UrlEncode(repository)}" +
                                                        $"&name={HttpUtility.UrlEncode(name)}", page, perPage);
        }

        public async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library)
        {
            _logger.Info($"Resolving package from Twinpack Server");

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package-resolve" +
                $"&distributor-name={library.DistributorName}&name={library.Name}&version={library.Version}"));

            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);

            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(int packageVersionId, bool includeBinary, string cachePath = null)
        {
            _logger.Info($"Retrieving package version from Twinpack Server");

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package-version" +
                $"&id={packageVersionId}&include-binary={(includeBinary ? 1 : 0)}"));

            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();


            var result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
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

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(string repository, string name, string version, string configuration, string branch, string target, bool includeBinary = false, string cachePath = null)
        {
            _logger.Info($"Retrieving package version from Twinpack Server");

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package-version" +
                $"&repository={HttpUtility.UrlEncode(repository)}" +
                $"&name={HttpUtility.UrlEncode(name)}" +
                $"&version={HttpUtility.UrlEncode(version)}" +
                $"&configuration={HttpUtility.UrlEncode(configuration)}" +
                $"&branch={HttpUtility.UrlEncode(branch)}" +
                $"&target={HttpUtility.UrlEncode(target)}" +
                $"&include-binary={(includeBinary ? 1 : 0)}"));

            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);
            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

 
            var result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
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

        public async Task<PackageGetResponse> GetPackageAsync(string repository, string packageName)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package" +
                $"&repository={HttpUtility.UrlEncode(repository)}" +
                $"&name={HttpUtility.UrlEncode(packageName)}"));

            _logger.Info($"Retrieving package from Twinpack Server");

            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageGetResponse> GetPackageAsync(int packageId)
        {
            _logger.Info($"Retrieving package from Twinpack Server");

            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(TwinpackUrl + $"/twinpack.php?controller=package&id={packageId}"));

            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
            if (result.Meta?.Message != null)
                throw new GetException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageVersionGetResponse> PutPackageVersionAsync(PackageVersionPatchRequest package)
        {
            var requestBodyJson = JsonSerializer.Serialize(package);

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/twinpack.php?controller=package-version"));
            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);
            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
            if (result.Meta?.Message != null)
                throw new PutException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<PackageGetResponse> PutPackageAsync(PackagePatchRequest package)
        {
            var requestBodyJson = JsonSerializer.Serialize(package);

            var request = new HttpRequestMessage(HttpMethod.Put, new Uri(TwinpackUrl + "/twinpack.php?controller=package"));
            request.Headers.Add("zgwk-username", Username);
            request.Headers.Add("zgwk-password", Password);
            request.Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
            if (result.Meta?.Message != null)
                throw new PutException(result.Meta.Message.ToString());

            return result;
        }

        public async Task<LoginPostResponse> LoginAsync(string username = null, string password = null)
        {
            var credentials = CredentialManager.ReadCredential("TwinpackServer");

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(TwinpackUrl + "/twinpack.php?controller=login"));
            request.Headers.Add("zgwk-username", username ?? credentials?.UserName);
            request.Headers.Add("zgwk-password", password ?? credentials?.Password);

            var response = await _client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            try
            {
                var result = JsonSerializer.Deserialize<LoginPostResponse>(responseBody);
                if (result.Meta?.Message != null)
                    throw new LoginException(result.Meta.Message.ToString());

                UserInfo = result;
                Username = username ?? credentials?.UserName;
                Password = password ?? credentials?.Password;

                if(!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                    CredentialManager.WriteCredential("TwinpackServer", Username, Password, CredentialPersistence.LocalMachine);

                return UserInfo;
            }
            catch (Exception ex)
            {
                UserInfo = new LoginPostResponse();
                Username = "";
                Password = "";
                CredentialManager.DeleteCredential("TwinpackServer");
                throw ex;
            }
        }

        public void Logout()
        {
            UserInfo = new LoginPostResponse();
            Username = "";
            Password = "";
            CredentialManager.DeleteCredential("TwinpackServer");

            try
            {
                CredentialManager.DeleteCredential("Twinpack");
            }
            catch (Exception) { }
        }

        public void InvalidateCache()
        {
            _client.Invalidate();
        }
    }
}
