using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Twinpack.Protocol.Api
{
    /// <summary>JSON body for updating package-level metadata on the Twinpack server (PUT package).</summary>
    public class PublishedPackageUpdate
    {
        [JsonPropertyName("package-id")]
        public int? PackageId { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("display-name")]
        public string DisplayName { get; set; }
        [JsonPropertyName("project-url")]
        public string ProjectUrl { get; set; }
        [JsonPropertyName("entitlement")]
        public string Entitlement { get; set; }
        [JsonPropertyName("icon-binary")]
        public string IconBinary { get; set; }
        [JsonPropertyName("icon-filename")]
        public string IconFilename { get; set; }
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("license-tmc-binary")]
        public string LicenseTmcBinary { get; set; }
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }
    }

    /// <summary>JSON body for PATCH on an existing published package version (compiled flag, release notes).</summary>
    public class PublishedPackageVersionUpdate
    {
        [JsonPropertyName("package-version-id")]
        public int? PackageVersionId { get; set; }
        [JsonPropertyName("compiled")]
        public int Compiled { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
    }

    /// <summary>One dependency entry when creating a new published package version.</summary>
    public class PublishedPackageVersionDependency
    {
        [JsonPropertyName("repository")]
        public string Repository { get; set; }
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("branch")]
        public string Branch { get; set; }
        [JsonPropertyName("target")]
        public string Target { get; set; }
        [JsonPropertyName("configuration")]
        public string Configuration { get; set; }
    }

    /// <summary>JSON body for publishing a new package version (POST package-version).</summary>
    public class PublishedPackageVersionCreate
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("target")]
        public string Target { get; set; }
        [JsonPropertyName("compiled")]
        public int Compiled { get; set; }
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        [JsonPropertyName("display-name")]
        public string DisplayName { get; set; }
        [JsonPropertyName("project-url")]
        public string ProjectUrl { get; set; }
        [JsonPropertyName("entitlement")]
        public string Entitlement { get; set; }
        [JsonPropertyName("branch")]
        public string Branch { get; set; }
        [JsonPropertyName("configuration")]
        public string Configuration { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }
        [JsonPropertyName("icon-filename")]
        public string IconFilename { get; set; }
        [JsonPropertyName("binary-download-url")]
        public string BinaryDownloadUrl { get; set; }
        [JsonPropertyName("license-tmc-binary")]
        public string LicenseTmcBinary { get; set; }
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }
        [JsonPropertyName("icon-binary")]
        public string IconBinary { get; set; }
        [JsonPropertyName("binary")]
        public string Binary { get; set; }
        [JsonPropertyName("dependencies")]
        public IEnumerable<PublishedPackageVersionDependency> Dependencies { get; set; }
    }

    /// <summary>JSON body for updating the download counter on a published package version.</summary>
    public class PublishedPackageVersionDownloadsUpdate
    {
        [JsonPropertyName("package-version-id")]
        public int? PackageVersionId { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }
    }
}
