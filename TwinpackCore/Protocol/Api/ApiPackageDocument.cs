using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Twinpack.Protocol.Api
{
    /// <summary>
    /// Package-level metadata from <c>IPackageServer</c> (Twinpack HTTP or NuGet-mapped feeds):
    /// identity, licensing, and the branch / target / configuration axes available for the package.
    /// </summary>
    public class PublishedPackage : Response
    {
        public PublishedPackage() { }

        public PublishedPackage(PublishedPackage package)
        {
            PackageId = package?.PackageId;
            Name = package?.Name;
            Title = package?.Title;
            Repository = package?.Repository;
            DistributorName = package?.DistributorName;
            DisplayName = package?.DisplayName;
            Description = package?.Description;
            Entitlement = package?.Entitlement;
            ProjectUrl = package?.ProjectUrl;
            IconUrl = package?.IconUrl;
            Authors = package?.Authors;
            License = package?.License;
            LicenseTmcBinary = package?.LicenseTmcBinary;
            LicenseBinary = package?.LicenseBinary;
            Branches = package?.Branches?.Any() == true ? new List<string>(package.Branches) : new List<string>();
            Targets = package?.Targets?.Any() == true ? new List<string>(package.Targets) : new List<string>();
            Configurations = package?.Configurations?.Any() == true ? new List<string>(package.Configurations) : new List<string>();
        }

        [JsonPropertyName("package-id")]
        public int? PackageId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("repository")]
        public string Repository { get; set; }
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }
        [JsonPropertyName("display-name")]
        public string DisplayName { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("entitlement")]
        public string Entitlement { get; set; }
        [JsonPropertyName("framework")]
        public string Framework { get; set; }
        [JsonPropertyName("project-url")]
        public string ProjectUrl { get; set; }
        [JsonPropertyName("icon-url")]
        public string IconUrl { get; set; }
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("license-tmc-binary")]
        public string LicenseTmcBinary { get; set; }
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }
        [JsonPropertyName("branches")]
        public List<string> Branches { get; set; }
        [JsonPropertyName("configurations")]
        public List<string> Configurations { get; set; }
        [JsonPropertyName("targets")]
        public List<string> Targets { get; set; }
        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }


        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasLicense { get { return !string.IsNullOrEmpty(License); } }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasLicenseTmcBinary { get { return !string.IsNullOrEmpty(LicenseTmcBinary); } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasLicenseBinary { get { return !string.IsNullOrEmpty(LicenseBinary); } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasProjectUrl { get { return !string.IsNullOrEmpty(ProjectUrl); } }
    }
}
