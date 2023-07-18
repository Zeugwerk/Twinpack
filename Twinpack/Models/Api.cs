using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Twinpack.Models
{
    public class PaginationHeader
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("self")]
        public string Self { get; set; }
        [JsonPropertyName("next")]
        public string Next { get; set; }
        [JsonPropertyName("prev")]
        public string Prev { get; set; }
    }
    public class CatalogGetRequest
    {
        [JsonPropertyName("search")]
        public string Search { get; set; }
    }
    public class CatalogItemGetResponse
    {
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
        [JsonPropertyName("display-name")]
        public string DisplayName { get; set; }
        [JsonPropertyName("versions")]
        public int Versions { get; set; }
        [JsonPropertyName("configurations")]
        public int Configurations { get; set; }
        [JsonPropertyName("targets")]
        public int Targets { get; set; }
        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }
        [JsonPropertyName("created")]
        public string Created { get; set; }
        [JsonPropertyName("modified")]
        public string Modified { get; set; }
        [JsonPropertyName("entitlement")]
        public string Entitlement { get; set; }
    }

    public class PackageVersionsItemGetResponse
    {
        [JsonPropertyName("package-version-id")]
        public int? PackageVersionId { get; set; }
        [JsonPropertyName("package-id")]
        public int? PackageId { get; set; }
        [JsonPropertyName("private")]
        public int Private { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("branch")]
        public string Branch { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("target")]
        public string Target { get; set; }
        [JsonPropertyName("configuration")]
        public string Configuration { get; set; }
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        [JsonPropertyName("entitlement")]
        public string Entitlement { get; set; }
        [JsonPropertyName("project-url")]
        public string ProjectUrl { get; set; }
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
    }

    public class PackageGetResponse
    {
        [JsonPropertyName("package-id")]
        public int? PackageId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
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
        [JsonPropertyName("project-url")]
        public string ProjectUrl { get; set; }
        [JsonPropertyName("icon-url")]
        public string IconUrl { get; set; }
    }

    public class PackageVersionGetResponse : PackageGetResponse
    {
        [JsonPropertyName("package-version-id")]
        public int? PackageVersionId { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("branch")]
        public string Branch { get; set; }
        [JsonPropertyName("target")]
        public string Target { get; set; }
        [JsonPropertyName("configuration")]
        public string Configuration { get; set; }
        [JsonPropertyName("compiled")]
        public int Compiled { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("binary")]
        public string Binary { get; set; }
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }

        public static bool operator ==(PackageVersionGetResponse lhs, PackageVersionGetResponse rhs)
        {
            return lhs.Name == rhs.Name && lhs.Version == rhs.Version && lhs.Target == rhs.Target && lhs.Configuration == rhs.Configuration && lhs.Branch == rhs.Branch;
        }

        public static bool operator !=(PackageVersionGetResponse lhs, PackageVersionGetResponse rhs)
        {
            return !(lhs == rhs);
        }

        public bool Equals(PackageVersionGetResponse o)
        {
            return this == o;
        }

    }

    public class PackagePatchRequest
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
    }

    public class PackageVersionPatchRequest
    {
        [JsonPropertyName("package-version-id")]
        public int? PackageVersionId { get; set; }
        [JsonPropertyName("compiled")]
        public int Compiled { get; set; }
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
    }

    public class PackageVersionPostRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
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
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }
        [JsonPropertyName("icon-binary")]
        public string IconBinary { get; set; }
        [JsonPropertyName("binary")]
        public string Binary { get; set; }
    }

    public class LoginPostResponse
    {
        [JsonPropertyName("user")]
        public string User { get; set; }
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }        
        [JsonPropertyName("configurations")]
        public List<string> Configurations { get; set; }
        [JsonPropertyName("targets")]
        public List<string> Targets { get; set; }
        [JsonPropertyName("entitlements")]
        public List<string> Entitlements { get; set; }
        [JsonPropertyName("flags")]
        public List<string> Flags { get; set; }       
    }    
}
