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
using System.Runtime.Remoting.Messaging;
using System.Runtime.CompilerServices;

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
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }
        public string ServerType { get; } = "NuGet Repository";
        public string Name { get; set; }
        public string UrlBase { get; set; }
        public string Url
        {
            get => UrlBase;
        }
        public string UrlRegister
        {
            get => UrlBase;
        }

        public string Username { get; set; }
        public string Password { get; set; }
        public LoginPostResponse UserInfo { get; set; }
        public bool LoggedIn { get { return UserInfo?.User != null; } }
        public bool Connected { get { return UserInfo?.UpdateVersion != null; } }

        public NugetServer(string name = "", string url = null)
        {
            Name = name;
            UrlBase = url;

            try
            {
                var credentials = CredentialManager.GetCredentials(UrlBase);
                Username = credentials?.UserName;
                Password = credentials?.Password;
            }
            catch
            {
                Username = null;
                Password = null;
            }
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

        public async Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, string branch, string configuration, string target, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

        }

        public async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task DownloadPackageVersionAsync(PackageVersionGetResponse packageVersion, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

        }

        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(PlcLibrary library, string branch, string configuration, string target, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

        }

        public async Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

        }

        public async Task<PackageVersionGetResponse> PutPackageVersionAsync(PackageVersionPatchRequest package, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

        }

        public async Task<PackageGetResponse> PutPackageAsync(PackagePatchRequest package, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

        }

        public async Task<LoginPostResponse> LoginAsync(string username = null, string password = null, CancellationToken cancellationToken = default)
        {
            // _client.Invalidate(); // clear the cache
            var credentials = CredentialManager.GetCredentials(UrlBase);

            Username = username ?? credentials?.UserName;
            Password = password ?? credentials?.Password;

            // reset token to get a new one
            if (UserInfo?.Token != null)
                UserInfo.Token = null;


            try
            {

            }
            catch
            {
                DeleteCredential();
                throw;
            }

            return new LoginPostResponse();
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

        public void Logout()
        {
            _logger.Info("Log out from NuGet Server");

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
            _logger.Info("Resetting NuGet Cache");
        }
    }
}
