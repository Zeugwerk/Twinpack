using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Twinpack.Models;
using Twinpack.Models.Api;
using System.Threading;

namespace Twinpack.Protocol
{
    public enum ChecksumMode
    {
        IgnoreMismatch,
        IgnoreMismatchAndFallack,
        Throw
    }

    public interface IPackageServer
    {
        string ServerType { get; }
        string Name { get; set; }
        string UrlBase { get; set; }
        string Url { get; }
        string UrlRegister { get; }
        Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default);
        Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, string branch = null, string configuration = null, string target = null, int page = 1, int perPage = 5, CancellationToken cancellationToken = default);
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
        bool Connected { get; }
        Task LogoutAsync();
        void InvalidateCache();
    }
}
