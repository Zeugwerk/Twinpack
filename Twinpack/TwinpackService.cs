using EnvDTE;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Models;
using Twinpack.Exceptions;

namespace Twinpack
{
    public class TwinpackService
    {
        public class PlcLibraryModel
        {
            public string Name { get; set; }
            public string Version { get; set; }
        }

        public class RetrieveVersionResult
        {
            public RetrieveVersionResult()
            {
                Success = false;
            }

            public bool Success { get; set; }
            public string ActualVersion { get; set; }
        }

 
        public static string TwinpackUrl = "https://zeugwerk.dev";
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }
        public static string DefaultUsername = "public";
        public static string DefaultPassword = "public";
        public static bool UseMainBranch = true;
        public static HashSet<PlcLibraryModel> InstalledLibraries { get; set; } = new HashSet<PlcLibraryModel>();

        private static bool Informed = false;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        static public ITcSysManager SystemManager(Solution solution, Models.ConfigProject plc)
        {

            return null;
        }

        static public HttpRequestMessage CreateHttpRequest(Uri uri, HttpMethod method, bool authorize = true)
        {
            // Set Credentials for download
            String username = Environment.GetEnvironmentVariable("ZGWK_USERNAME") ?? DefaultUsername;
            String password = Environment.GetEnvironmentVariable("ZGWK_PASSWORD") ?? DefaultPassword;
            if (!Informed)
            {
                if (username == DefaultUsername && password == DefaultPassword)
                {
                    Informed = true;
                    _logger.Debug(" Using public credentials, access is limited to public areas. " +
                                      "Set ZGWK_USERNAME and ZGWK_PASSWORD environment variables to access restricted areas!");
                }
                else
                {
                    Informed = true;
                    _logger.Debug($"Using {username}/*** credentials");
                }
            }

            string credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(username + ":" + password));

            // create Request
            HttpRequestMessage request = new HttpRequestMessage(method, uri);
            if (authorize)
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            return request;
        }

        static public async Task<bool> IsUriAvailableAsync(Uri uri, bool authorize = true)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var request = CreateHttpRequest(uri, HttpMethod.Head, authorize);
                    var response = await client.SendAsync(request);
                    var headers = response.Content.Headers;
                    if (response.StatusCode == System.Net.HttpStatusCode.OK && (headers.ContentType == null || headers.ContentType.MediaType != "text/html"))
                    {
                        _logger.Trace($"Uri IS     available ({uri})");
                        return true;
                    }

                    _logger.Trace($"Uri IS NOT available ({uri})");
                }
                catch
                {
                    _logger.Trace($"Uri IS NOT available ({uri})");
                }

                return false;
            }
        }

        public static string FindTwincatSubfolder(string used_tcversion, string targetPath, List<string> supported_tcversions = null)
        {
            var used = used_tcversion.Split('.');
            if (supported_tcversions != null)
                while (used.Length > 0 && !supported_tcversions.Exists(x => string.Join(".", used).ToLower() == x.ToLower()))
                    Array.Resize(ref used, used.Length - 1);
            else
                while (used.Length > 0 && !Directory.Exists($@"{targetPath}\{string.Join(".", used).ToLower()}"))
                    Array.Resize(ref used, used.Length - 1);

            if (used.Length == 0)
                return null;

            return string.Join(".", used);
        }

        public static string FindLibraryFilePathWithoutExtension(string tcversion, string referencename, string referenceversion, string targetPath)
        {
            targetPath = targetPath ?? TwinpackService.DefaultLibraryCachePath;

            if (referenceversion.Split('.').Length == 4)
                return string.Join("\\", new string[] { targetPath, $"{TwinpackService.FindTwincatSubfolder(tcversion, targetPath)}", $"{referencename}_{referenceversion}." });

            return Directory.GetFiles(string.Join("\\", new string[] { targetPath, $"{TwinpackService.FindTwincatSubfolder(tcversion, targetPath)}" }), $"{referencename}_*library", SearchOption.TopDirectoryOnly)
                .Select(x => new FileInfo(x))
                .OrderBy(x => x.CreationTime)
                .Select(x => Path.ChangeExtension(x.FullName, ""))
                .FirstOrDefault();
        }

        static public bool IsCached(string used_tcversion, List<string> references, string version, string cachePath)
        {
            List<string> paths = new List<string>();
            if (version == null)
                paths = references.Select(x => $@"{used_tcversion}\{x.Split('=').First()}_{x.Split('=').Last()}").ToList();
            else
                paths = references.Select(x => $@"{used_tcversion}\{x}_{version}").ToList();

            foreach (var filename in paths)
            {
                if (!File.Exists($@"{cachePath}\{filename}.compiled-library") &&
                    !File.Exists($@"{cachePath}\{filename}.library"))
                {
                    _logger.Info($"One or more references for TwinCAT {used_tcversion} not found in cache!");
                    return false;
                }
            }

            return true;
        }

        static public async Task PullAsync(string username, string password, string configuration, string branch, string target)
        {
            var filePath = $@".Zeugwerk\libraries\{target}";
            Directory.CreateDirectory(filePath);
            var config = ConfigFactory.Load();

            _logger.Info($"Pulling packages required by {config.Solution} from Twinpack Server");
            var plcs = config.Projects.SelectMany(x => x.Plcs);

            foreach (var plc in plcs)
            {
                foreach(var package in plc.Packages ?? new List<ConfigPlcPackage>())
                {
                    _logger.Info($"Downloading {package.Name} (version: {package.Version}, configuration: {configuration}, branch: {package.Branch}, target: {target})");
                    using (HttpClient client = new HttpClient())
                    {
                        var request = CreateHttpRequest(
                            new Uri(TwinpackUrl + $"/twinpack.php?controller=package-version" +
                            $"&repository={HttpUtility.UrlEncode(package.Repository)}" +
                            $"&name={HttpUtility.UrlEncode(package.Name)}" +
                            $"&version={HttpUtility.UrlEncode(package.Version)}" +
                            $"&configuration={HttpUtility.UrlEncode(configuration)}" +
                            $"&branch={HttpUtility.UrlEncode(branch)}" +
                            $"&target={HttpUtility.UrlEncode(target)}"), HttpMethod.Get, authorize: true);

                        request.Headers.Add("zgwk-username", username);
                        request.Headers.Add("zgwk-password", password);
                        var response = await client.SendAsync(request);
                        var responseBody = await response.Content.ReadAsStringAsync();
                        
                        try
                        {
                            var packageVersion = JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
                            var extension = packageVersion.Compiled == 1 ? ".compiled-library" : ".library";
                            File.WriteAllText($@"{filePath}\{packageVersion.Name}_{packageVersion.Version}.library", Encoding.ASCII.GetString(Convert.FromBase64String(packageVersion.Binary)));
                        }
                        catch(Exception)
                        {
                            JsonElement responseJsonBody = JsonSerializer.Deserialize<dynamic>(responseBody);
                            JsonElement message = new JsonElement();
                            if (responseJsonBody.TryGetProperty("message", out message))
                            {
                                throw new PostException(message.ToString());
                            }
                        }
                    }
                }
            }
        }

        static public async Task PushAsync(string username, string password, string configuration, string branch, string target, string notes, bool compiled)
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
                var fileName = $@".Zeugwerk\libraries\{target}\{plc.Name}_{plc.Version}.{suffix}";
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
                string binary = Convert.ToBase64String(File.ReadAllBytes($@".Zeugwerk\libraries\{target}\{plc.Name}_{plc.Version}.{suffix}"));
                string licenseBinary = (!File.Exists(plc.LicenseFile) || string.IsNullOrEmpty(plc.LicenseFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.LicenseFile));
                var requestBody = new PackageVersionPostRequest()
                {
                    Name = plc.Name,
                    Version = plc.Version,
                    Binary = binary,
                    Target = target,
                    License = plc.License,
                    Description = plc.Description,
                    Authors = plc.Authors,
                    Entitlement = plc.Entitlement,
                    ProjectUrl = plc.ProjectUrl,
                    DisplayName = plc.DisplayName,
                    IconUrl = plc.IconUrl,                    
                    Branch = branch,
                    Configuration = configuration,
                    Compiled = compiled ? 1 : 0,
                    Notes = notes,
                    LicenseBinary = licenseBinary
                };

                var requestBodyJson = JsonSerializer.Serialize(requestBody);
                _logger.Debug($"Pushing {plc.Name}: {requestBodyJson}");

                using (HttpClient client = new HttpClient())
                {
                    var request = CreateHttpRequest(new Uri(TwinpackUrl + "/twinpack.php?controller=package-version"), HttpMethod.Post, authorize: true);
                    request.Headers.Add("zgwk-username", username);
                    request.Headers.Add("zgwk-password", password);
                    request.Content = new StreamContent(
                        new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

                    var response = await client.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    JsonElement packageVersion = JsonSerializer.Deserialize<dynamic>(responseBody);
                    JsonElement message = new JsonElement();
                    if(packageVersion.TryGetProperty("message", out message))
                        throw new PostException(message.ToString());
                }
            }
        }

        static public async Task<IEnumerable<T>> QueryWithPagination<T>(string username, string password, string endpoint, int page=1, int perPage=5)
        {
            var results = new List<T>();
            var query = $"/{endpoint}";
            var uri = new Uri(TwinpackUrl + query + $"&page={page}&per_page={perPage}");

            using (HttpClient client = new HttpClient())
            {
                var hasNextPage = true;
                while (hasNextPage)
                {
                    var request = CreateHttpRequest(uri, HttpMethod.Get, authorize: true);
                    request.Headers.Add("zgwk-username", username);
                    request.Headers.Add("zgwk-password", password);

                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var data = await response.Content.ReadAsStringAsync();
                    results.AddRange(JsonSerializer.Deserialize<List<T>>(data));

                    if (results.Count() >= perPage)
                        return results.Take(perPage);

                    var linkHeader = response.Headers.GetValues("Link");
                    if (linkHeader.Any())
                    {
                        var h = Regex.Unescape(linkHeader.First());

                        try
                        {
                            var pagination = JsonSerializer.Deserialize<PaginationHeader>(h);
                            if (pagination.Next == null)
                            {
                                hasNextPage = false;
                                break;
                            }
    
                            uri = new Uri(pagination.Next);
                        }
                        catch(Exception)
                        {
                            JsonElement responseBody = JsonSerializer.Deserialize<dynamic>(data);
                            JsonElement message = new JsonElement();
                            if (responseBody.TryGetProperty("message", out message))
                            {
                                throw new QueryException(query, message.ToString());
                            }
                        }                            
                    }
                }
            }

            return results;
        }

        static public async Task<IEnumerable<CatalogItemGetResponse>> GetCatalogAsync(string username, string password, string search, int page=1, int perPage=5)
        {
            _logger.Info($"Retrieving package catalog of Twinpack Server");
            return await QueryWithPagination<CatalogItemGetResponse>(username, password,
                                                $"twinpack.php?controller=catalog" +
                                                $"&search={search}", page, perPage);
        }

        static public async Task<IEnumerable<PackageVersionsItemGetResponse>> GetPackageVersionsAsync(string username, string password, int packageId, int page=1, int perPage=5)
        {
            _logger.Info($"Retrieving package catalog of Twinpack Server");
            return await QueryWithPagination<PackageVersionsItemGetResponse>(username, password,
                                            $"twinpack.php?controller=package-versions" +
                                            $"&id={packageId}", page, perPage);
        }
        
        static public async Task<IEnumerable<PackageVersionsItemGetResponse>> GetPackageVersionsAsync(string username, string password, string repository, string name, int page=1, int perPage=5)
        {
            _logger.Info($"Retrieving package catalog of Twinpack Server");
            return await QueryWithPagination<PackageVersionsItemGetResponse>(username, password,
                                            $"twinpack.php?controller=package-versions" +
                                                        $"&repository={HttpUtility.UrlEncode(repository)}" +
                                                        $"&name={HttpUtility.UrlEncode(name)}", page, perPage);
        }

        static public async Task<PackageVersionGetResponse> GetPackageVersionAsync(string username, string password, int packageVersionId)
        {
            _logger.Info($"Retrieving package version from Twinpack Server");
            using (HttpClient client = new HttpClient())
            {
                var request = CreateHttpRequest(new Uri(TwinpackUrl + $"/twinpack.php?controller=package-version&id={packageVersionId}"), HttpMethod.Get, authorize: true);
                request.Headers.Add("zgwk-username", username);
                request.Headers.Add("zgwk-password", password);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                try
                {
                    return JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
                }
                catch (Exception ex)
                {
                    JsonElement responseJsonBody = JsonSerializer.Deserialize<dynamic>(responseBody);
                    JsonElement message = new JsonElement();
                    if (responseJsonBody.TryGetProperty("message", out message))
                    {
                        throw new GetException(message.ToString());
                    }
                    else
                    {
                        throw ex;
                    }
                }
            }
        }

        static public async Task<PackageGetResponse> GetPackageAsync(string username, string password, int packageId)
        {
            _logger.Info($"Retrieving package from Twinpack Server");
            using (HttpClient client = new HttpClient())
            {
                var request = CreateHttpRequest(new Uri(TwinpackUrl + $"/twinpack.php?controller=package&id={packageId}"), HttpMethod.Get, authorize: true);
                request.Headers.Add("zgwk-username", username);
                request.Headers.Add("zgwk-password", password);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                try
                {
                    return JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
                }
                catch(Exception ex)
                {
                    JsonElement responseJsonBody = JsonSerializer.Deserialize<dynamic>(responseBody);
                    JsonElement message = new JsonElement();
                    if (responseJsonBody.TryGetProperty("message", out message))
                        throw new GetException(message.ToString());
                    else
                        throw ex;
                }
            }
        }

        static public async Task<PackageVersionGetResponse> PutPackageVersionAsync(string username, string password, PackageVersionPatchRequest package)
        {
            var requestBodyJson = JsonSerializer.Serialize(package);

            using (HttpClient client = new HttpClient())
            {
                var request = CreateHttpRequest(new Uri(TwinpackUrl + "/twinpack.php?controller=package-version"), HttpMethod.Put, authorize: true);
                request.Headers.Add("zgwk-username", username);
                request.Headers.Add("zgwk-password", password);
                request.Content = new StreamContent(
                    new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                try
                {
                    return JsonSerializer.Deserialize<PackageVersionGetResponse>(responseBody);
                }
                catch (Exception ex)
                {
                    JsonElement responseJsonBody = JsonSerializer.Deserialize<dynamic>(responseBody);
                    JsonElement message = new JsonElement();
                    if (responseJsonBody.TryGetProperty("message", out message))
                        throw new PutException(message.ToString());
                    else
                        throw ex;
                }


            }

            return null;
        }


        static public async Task<PackageGetResponse> PutPackageAsync(string username, string password, PackagePatchRequest package)
        {        
            var requestBodyJson = JsonSerializer.Serialize(package);

            using (HttpClient client = new HttpClient())
            {
                var request = CreateHttpRequest(new Uri(TwinpackUrl + "/twinpack.php?controller=package"), HttpMethod.Put, authorize: true);
                request.Headers.Add("zgwk-username", username);
                request.Headers.Add("zgwk-password", password);
                request.Content = new StreamContent(
                    new MemoryStream(Encoding.UTF8.GetBytes(requestBodyJson)));

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                try
                {
                    return JsonSerializer.Deserialize<PackageGetResponse>(responseBody);
                }
                catch (Exception)
                {
                    JsonElement responseJsonBody = JsonSerializer.Deserialize<dynamic>(responseBody);
                    JsonElement message = new JsonElement();
                    if (responseJsonBody.TryGetProperty("message", out message))
                        throw new PutException(message.ToString());
                }
            }

            return null;
        } 

        
        
        static public async Task<bool> LoginAsync(string username, string password)
        {
            using (HttpClient client = new HttpClient())
            {
                var request = CreateHttpRequest(new Uri(TwinpackUrl + "/twinpack.php?controller=login"), HttpMethod.Post, authorize: true);
                request.Headers.Add("zgwk-username", username);
                request.Headers.Add("zgwk-password", password);

                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                JsonElement responseBody = JsonSerializer.Deserialize<dynamic>(content);
                JsonElement message = new JsonElement();
                if(responseBody.TryGetProperty("message", out message))
                    throw new LoginException(message.ToString());
            }

            return true;
        }
    }
}
