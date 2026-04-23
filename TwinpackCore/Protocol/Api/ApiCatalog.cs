using System.Text.Json.Serialization;

namespace Twinpack.Protocol.Api
{
    public class CatalogGetRequest
    {
        [JsonPropertyName("search")]
        public string Search { get; set; }
    }

    /// <summary>
    /// One row from catalog search (<c>GetCatalogAsync</c>): enough to pick a package before loading full
    /// <see cref="PublishedPackage"/> / <see cref="PublishedPackageVersion"/> details.
    /// </summary>
    public class CatalogPackageSummary : Response
    {
        public CatalogPackageSummary()
        {

        }
        public CatalogPackageSummary(CatalogPackageSummary obj)
        {
            PackageId = obj.PackageId;
            Name = obj.Name;
            Repository = obj.Repository;
            DistributorName = obj.DistributorName;
            Description = obj.Description;
            IconUrl = obj.IconUrl;
            RuntimeLicense = obj.RuntimeLicense;
            DisplayName = obj.DisplayName;
            Downloads = obj.Downloads;
            Created = obj.Created;
            Modified = obj.Modified;
        }

        [JsonPropertyName("package-id")]
        public int? PackageId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("repository")]
        public string Repository { get; set; }
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("icon-url")]
        public string IconUrl { get; set; }
        [JsonPropertyName("runtime-license")]
        public int? RuntimeLicense { get; set; }
        [JsonPropertyName("display-name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("downloads")]
        public int? Downloads { get; set; }
        [JsonPropertyName("created")]
        public string Created { get; set; }
        [JsonPropertyName("modified")]
        public string Modified { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasRuntimeLicense { get { return RuntimeLicense > 0; } }

#if !NETSTANDARD2_1_OR_GREATER
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public System.Windows.Media.Imaging.BitmapImage Icon { get; set; }
#endif
    }
}
