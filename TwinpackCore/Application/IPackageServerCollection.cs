using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;
using Twinpack.Protocol.Api;

namespace Twinpack.Application
{
    /// <summary>
    /// Ordered list of package sources with orchestration for search, resolve, fetch, pull, and download.
    /// Implemented by <see cref="PackageServerCollection"/>.
    /// </summary>
    public interface IPackageServerCollection : IList<IPackageServer>
    {
        void InvalidateCache();

        Task LoginAsync(string username, string password);

        IAsyncEnumerable<PackageItem> SearchAsync(string filter = null, int? maxPackages = null, int batchSize = 5, CancellationToken token = default);

        Task<PublishedPackageVersion> ResolvePackageAsync(string name, ResolvePackageOptions options = default, CancellationToken cancellationToken = default);

        Task<PackageItem> FetchPackageAsync(PlcPackageReference item, bool includeMetadata = false, IAutomationInterface automationInterface = null, CancellationToken cancellationToken = default);

        Task<PackageItem> FetchPackageAsync(string projectName, string plcName, PlcPackageReference item, bool includeMetadata = false, IAutomationInterface automationInterface = null, CancellationToken cancellationToken = default);

        Task<PackageItem> FetchPackageAsync(IPackageServer packageServer, string projectName, string plcName, PlcPackageReference item, bool includeMetadata = false, IAutomationInterface automationInterface = null, CancellationToken cancellationToken = default);

        Task PullAsync(Config config, bool skipInternalPackages = false, IEnumerable<PlcPackageReference> filter = null, string cachePath = null, CancellationToken cancellationToken = default);

        Task<List<PackageItem>> ResolvePackageDependenciesAsync(PackageItem package, IAutomationInterface automationInterface, CancellationToken cancellationToken = default);

        Task<bool> DownloadPackageVersionAsync(PackageItem package, string downloadPath = null, CancellationToken cancellationToken = default);
    }
}
