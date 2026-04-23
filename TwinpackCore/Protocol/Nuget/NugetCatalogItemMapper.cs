using NuGet.Protocol.Core.Types;
using Twinpack.Protocol.Api;

namespace Twinpack.Protocol
{
    /// <summary>Maps NuGet search metadata to Twinpack catalog DTOs (transport shape).</summary>
    internal static class NugetCatalogItemMapper
    {
        public static CatalogPackageSummary FromSearchMetadata(IPackageSearchMetadata metadata, string fallbackIconUrl)
        {
            return new CatalogPackageSummary
            {
                PackageId = null,
                Name = metadata.Identity.Id,
                DistributorName = metadata.Authors,
                Description = metadata.Description,
                IconUrl = metadata.IconUrl?.ToString() ?? fallbackIconUrl,
                RuntimeLicense = 0,
                DisplayName = metadata.Identity.Id,
                Downloads = metadata.DownloadCount.HasValue && metadata.DownloadCount.Value > 0 ? ((int?)metadata.DownloadCount.Value) : null,
                Created = metadata.Published?.ToString() ?? "Unknown",
                Modified = metadata.Published?.ToString() ?? "Unknown"
            };
        }
    }
}
