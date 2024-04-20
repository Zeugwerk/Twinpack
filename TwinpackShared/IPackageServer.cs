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

namespace Twinpack
{
    public enum ChecksumMode
    {
        IgnoreMismatch,
        IgnoreMismatchAndFallack,
        Throw
    }

    public interface IPackageServer
    {
        string UrlBase { get; }
        string Url { get; }
        string UrlRegister { get; }

        Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default);
        Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, int page = 1, int perPage = 5, CancellationToken cancellationToken = default);
        Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, string branch, string configuration, string target, int page = 1, int perPage = 5, CancellationToken cancellationToken = default);
        Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default);
        Task DownloadPackageVersionAsync(PackageVersionGetResponse packageVersion, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default);
        Task<PackageVersionGetResponse> GetPackageVersionAsync(PlcLibrary library, string branch, string configuration, string target, CancellationToken cancellationToken = default);
        Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName, CancellationToken cancellationToken = default);
        Task<PackageGetResponse> PutPackageAsync(PackagePatchRequest package, CancellationToken cancellationToken = default);
        Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion, CancellationToken cancellationToken = default);
        Task<PackageVersionGetResponse> PutPackageVersionAsync(PackageVersionPatchRequest package, CancellationToken cancellationToken = default);
        Task<LoginPostResponse> LoginAsync(string username = null, string password = null, CancellationToken cancellationToken = default);
        string Username { get; set; }
        string Password { get; set; }
        LoginPostResponse UserInfo { get; }
        bool LoggedIn { get; }
        void Logout();
        void InvalidateCache();
    }
}
