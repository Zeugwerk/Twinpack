using System.Collections.Generic;
using System.Threading.Tasks;
using Twinpack.Models;
using Twinpack.Protocol.Api;
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
        bool Enabled { get; set; }
        string UrlBase { get; set; }
        string Url { get; }
        string UrlRegister { get; }
        Task<PaginatedBatch<CatalogPackageSummary>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default);
        Task<PaginatedBatch<PublishedPackageVersion>> GetPackageVersionsAsync(PackageReferenceKey library, string branch = null, string configuration = null, string target = null, int page = 1, int perPage = 5, CancellationToken cancellationToken = default);
        Task<PublishedPackageVersion> ResolvePackageVersionAsync(PackageReferenceKey library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default);
        Task DownloadPackageVersionAsync(PublishedPackageVersion packageVersion, ChecksumMode checksumMode, string downloadPath = null, CancellationToken cancellationToken = default);
        Task<PublishedPackageVersion> GetPackageVersionAsync(PackageReferenceKey library, string branch, string configuration, string target, CancellationToken cancellationToken = default);
        Task<PublishedPackage> GetPackageAsync(string distributorName, string packageName, CancellationToken cancellationToken = default);
        Task<PublishedPackage> PutPackageAsync(PublishedPackageUpdate package, CancellationToken cancellationToken = default);
        Task<PublishedPackageVersion> PostPackageVersionAsync(PublishedPackageVersionCreate packageVersion, CancellationToken cancellationToken = default);
        Task<PublishedPackageVersion> PutPackageVersionAsync(PublishedPackageVersionUpdate package, CancellationToken cancellationToken = default);
        Task<TwinpackLoginResult> LoginAsync(string username = null, string password = null, CancellationToken cancellationToken = default);
        string Username { get; set; }
        string Password { get; set; }
        TwinpackLoginResult UserInfo { get; }
        bool LoggedIn { get; }
        bool Connected { get; }
        Task LogoutAsync();
        void InvalidateCache();
    }
}
