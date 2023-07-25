using System.Collections.Generic;
using System.Linq;
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
        public CatalogItemGetResponse()
        {

        }
        public CatalogItemGetResponse(CatalogItemGetResponse obj)
        {
            PackageId = obj.PackageId;
            Name = obj.Name;
            Repository = obj.Repository;
            DistributorName = obj.DistributorName;
            Description = obj.Description;
            IconUrl = obj.IconUrl;
            DisplayName = obj.DisplayName;
            Versions = obj.Versions;
            Configurations = obj.Configurations;
            Targets = obj.Targets;
            Downloads = obj.Downloads;
            Created = obj.Created;
            Modified = obj.Modified;
            Entitlement = obj.Entitlement;
            Branches = obj.Branches;
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
        [JsonPropertyName("branches")]
        public List<string> Branches { get; set; }        
    }

    public class PackageVersionsItemGetResponse
    {
        [JsonPropertyName("package-version-id")]
        public int? PackageVersionId { get; set; }
        [JsonPropertyName("package-id")]
        public int? PackageId { get; set; }
        [JsonPropertyName("private")]
        public int Private { get; set; }
        [JsonPropertyName("repository")]
        public string Repository { get; set; }
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
        [JsonPropertyName("authors")]
        public string Authors { get; set; }
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasLicense { get { return !string.IsNullOrEmpty(License); } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasLicenseBinary { get { return !string.IsNullOrEmpty(LicenseBinary); } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasProjectUrl { get { return !string.IsNullOrEmpty(ProjectUrl); } }
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
        [JsonPropertyName("binary")]
        public string Binary { get; set; }
        [JsonPropertyName("dependencies")]
        public IEnumerable<PackageVersionGetResponse> Dependencies { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasDependencies { get { return Dependencies?.Any() == true; } }

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
        [JsonPropertyName("authors")]
        public string Authors { get; set; }        
        [JsonPropertyName("license")]
        public string License { get; set; }
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }
    }

    public class PackageVersionPatchRequest
    {
        [JsonPropertyName("package-version-id")]
        public int? PackageVersionId { get; set; }
        [JsonPropertyName("compiled")]
        public int Compiled { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
    }

    public class PackageVersionDependencyPostRequest
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
        [JsonPropertyName("dependencies")]
        public IEnumerable<PackageVersionDependencyPostRequest> Dependencies { get; set; }
    }

    public class LoginPostResponse
    {
        public class Configuration
        {
            [JsonPropertyName("configuration")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }
        }

        public class Target
        {
            [JsonPropertyName("configuration")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }
        }

        [JsonPropertyName("user")]
        public string User { get; set; }
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }        
        [JsonPropertyName("configurations")]
        public List<Configuration> Configurations { get; set; }
        [JsonPropertyName("targets")]
        public List<Target> Targets { get; set; }
        [JsonPropertyName("entitlements")]
        public List<string> Entitlements { get; set; }
        [JsonPropertyName("flags")]
        public List<string> Flags { get; set; }       
    }    
}
