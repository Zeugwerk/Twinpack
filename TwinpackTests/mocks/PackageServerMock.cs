using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using Twinpack.Protocol;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace TwinpackTests
{
    public class PackageServerMock : IPackageServer
    {
        public List<CatalogItemGetResponse> CatalogItems { get; set; } = new List<CatalogItemGetResponse>();
        public List<PackageVersionGetResponse> PackageVersionItems { get; set; } = new List<PackageVersionGetResponse>();
        public List<PackageVersionGetResponse> DownloadedPackageVersions { get; set; } = new List<PackageVersionGetResponse>();

        public string ServerType => throw new NotImplementedException();
        public string Name { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string UrlBase { get; set; }
        public string Url => throw new NotImplementedException();
        public string UrlRegister => throw new NotImplementedException();
        public string Username { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Password { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public LoginPostResponse UserInfo => throw new NotImplementedException();
        public bool LoggedIn => throw new NotImplementedException();
        public bool Connected { get; set; }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task DownloadPackageVersionAsync(PackageVersionGetResponse packageVersion, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (PackageVersionItems.Any(x =>
                    x.Name == packageVersion.Name &&
                    (x.Version == packageVersion.Version || packageVersion.Version == null) &&
                    (x.Branch == packageVersion.Branch || packageVersion.Branch == null) &&
                    (x.Configuration == packageVersion.Configuration || packageVersion.Configuration == null) &&
                    (x.Target == packageVersion.Target || packageVersion.Target == null)))
            {
                DownloadedPackageVersions.Add(packageVersion);
                return;
            }

            throw new Exception("Package is not available on this server");
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var items = CatalogItems.Where(x => search == null || x.Name.Equals(search, StringComparison.OrdinalIgnoreCase)).ToList();

            page = page - 1;
            if (page * perPage + perPage < items.Count)
                return new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(items.GetRange(page * perPage, perPage), true);
            else if (page * perPage < items.Count && items.Any())
                return new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(items.GetRange(page * perPage,  (items.Count - page * perPage > 0) ? (items.Count - page * perPage) : 1), false);

            return new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(new List<CatalogItemGetResponse> { }, false);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<PackageGetResponse> GetPackageAsync(string distributorName, string packageName, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return PackageVersionItems
                .Where(x =>
                    x.Name == packageName &&
                    x.DistributorName == distributorName)
                .OrderByDescending(x => x.Version == null ? new Version(9, 9, 9, 9) : new Version(x.Version))
                .Select(x => new PackageGetResponse { Name = packageName, DistributorName = distributorName, Branches = new List<string> { "main", "release/1.0" } }).FirstOrDefault() ?? new PackageGetResponse();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<PackageVersionGetResponse> GetPackageVersionAsync(PlcLibrary library, string branch, string configuration, string target, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return PackageVersionItems
                .Where(x =>
                    x.Name == library.Name &&
                    (x.Version == library.Version || library.Version == null) &&
                    (x.Branch == branch || branch == null) &&
                    (x.Configuration == configuration || configuration == null) &&
                    (x.Target == target || target == null))
                .OrderByDescending(x => x.Version == null ? new Version(9, 9, 9, 9) : new Version(x.Version))
                .FirstOrDefault() ?? new PackageVersionGetResponse();
        }

        public Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, string branch = null, string configuration = null, string target = null, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void InvalidateCache()
        {
        }

        public Task<LoginPostResponse> LoginAsync(string username = null, string password = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task LogoutAsync()
        {
            throw new NotImplementedException();
        }

        public Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PackageGetResponse> PutPackageAsync(PackagePatchRequest package, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<PackageVersionGetResponse> PutPackageVersionAsync(PackageVersionPatchRequest package, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if(string.IsNullOrEmpty(library.Version))
                return PackageVersionItems.Where(x => x.Name == library.Name)
                .OrderByDescending(x => preferredBranch == x.Branch)
                .ThenByDescending(x => (string.IsNullOrEmpty(library.Version) || new Version(library.Version) == new Version(x.Version)))
                .ThenByDescending(x => x.Version == null ? new Version(9, 9, 9, 9) : new Version(x.Version))
                .FirstOrDefault();
            else
                return PackageVersionItems.Where(x => x.Name == library.Name)
                .OrderByDescending(x => preferredBranch == x.Branch)
                .ThenByDescending(x => (string.IsNullOrEmpty(library.Version) || new Version(library.Version) == new Version(x.Version)))
                .ThenByDescending(x => x.Version == null ? new Version(9,9,9,9) : new Version(x.Version))
                .FirstOrDefault();
        }
    }
}
