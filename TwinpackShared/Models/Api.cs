using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Linq;

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

    public class Response
    {
        public class ResponseMeta
        {
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }

        [JsonPropertyName("_meta")]
        public ResponseMeta Meta { get; set; }
    }
    
    public class CatalogItemGetResponse : Response
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
        public int RuntimeLicense { get; set; }
        [JsonPropertyName("display-name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }
        [JsonPropertyName("created")]
        public string Created { get; set; }
        [JsonPropertyName("modified")]
        public string Modified { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasRuntimeLicense { get { return RuntimeLicense > 0; } }
    }
    public class PackageGetResponse : Response
    {
        public PackageGetResponse() { }

        public PackageGetResponse(PackageGetResponse package)
        {
            PackageId = package.PackageId;
            Name = package.Name;
            Title = package.Title;
            Repository = package.Repository;
            DistributorName = package.DistributorName;
            DisplayName = package.DisplayName;
            Description = package.Description;
            Entitlement = package.Entitlement;
            ProjectUrl = package.ProjectUrl;
            IconUrl = package.IconUrl;
            Authors = package.Authors;
            License = package.License;
            LicenseTmcBinary = package.LicenseTmcBinary;
            LicenseBinary = package.LicenseBinary;
            Branches = new List<string>(package.Branches);
            Targets = new List<string>(package.Targets);
            Configurations = new List<string>(package.Configurations);
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

    public class PackageVersionGetResponse : PackageGetResponse
    {
        public PackageVersionGetResponse()
        {

        }

        public PackageVersionGetResponse(PackageVersionGetResponse packageVersion) : base(packageVersion)
        {
            PackageVersionId = packageVersion.PackageVersionId;
            Version = packageVersion.Version;
            Branch = packageVersion.Branch;
            Target = packageVersion.Target;
            Configuration = packageVersion.Configuration;
            Compiled = packageVersion.Compiled;
            Notes = packageVersion.Notes;
            Binary = packageVersion.Binary;
            BinaryDownloadUrl = packageVersion.BinaryDownloadUrl;
            BinarySha256 = packageVersion.BinarySha256;
            Dependencies = new List<PackageVersionGetResponse>(packageVersion.Dependencies);
        }

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
        [JsonPropertyName("binary-download-url")]
        public string BinaryDownloadUrl { get; set; }
        [JsonPropertyName("binary-sha256")]
        public string BinarySha256 { get; set; }        
        [JsonPropertyName("dependencies")]
        public IEnumerable<PackageVersionGetResponse> Dependencies { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasDependencies { get { return Dependencies?.Any() == true; } }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string LicenseTmcText
        {
            get
            {
                if (!HasLicenseTmcBinary)
                    return null;

                return Encoding.ASCII.GetString(Convert.FromBase64String(LicenseTmcBinary));
            }
        }

        public static bool operator ==(PackageVersionGetResponse lhs, PackageVersionGetResponse rhs)
        {
            return lhs?.Name == rhs?.Name && lhs?.Title == rhs?.Title && lhs?.Version == rhs?.Version && lhs?.Target == rhs?.Target && lhs?.Configuration == rhs?.Configuration && lhs?.Branch == rhs?.Branch;
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
        [JsonPropertyName("license-tmc-binary")]
        public string LicenseTmcBinary { get; set; }
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
        [JsonPropertyName("license-tmc-binary")]
        public string LicenseTmcBinary { get; set; }
        [JsonPropertyName("license-binary")]
        public string LicenseBinary { get; set; }
        [JsonPropertyName("icon-binary")]
        public string IconBinary { get; set; }
        [JsonPropertyName("binary")]
        public string Binary { get; set; }
        [JsonPropertyName("dependencies")]
        public IEnumerable<PackageVersionDependencyPostRequest> Dependencies { get; set; }
    }

    public class LoginPostResponse : Response
    {
        public class Configuration
        {
            [JsonPropertyName("configuration")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPublic { get { return Public == 1; } }

            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPrivate { get { return Public == 0; } }
        }

        public class Target
        {
            [JsonPropertyName("target")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPublic { get { return Public == 1; } }
            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPrivate { get { return Public == 0; } }
        }

        public class Entitlement
        {
            [JsonPropertyName("entitlement")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPublic { get { return Public == 1; } }
            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPrivate { get { return Public == 0; } }
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
        public List<Entitlement> Entitlements { get; set; }
        [JsonPropertyName("flags")]
        public List<string> Flags { get; set; }
        [JsonPropertyName("update-version")]
        public string UpdateVersion { get; set; }
        [JsonPropertyName("update-url")]
        public string UpdateUrl { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasCompiledLibraryCapability { get { return Flags?.Contains("FLAG_COMPILED") == true; } }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasLicenseTmcCapability { get { return Flags?.Contains("FLAG_RUNTIME_LICENSE") == true; } }
    }

    public class NotificationsGetResponse : Response
    {
        [JsonPropertyName("latest-version")]
        public string LatestVersion { get; set; }
    }
}
