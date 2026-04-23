using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Twinpack.Protocol.Api
{
    /// <summary>
    /// One concrete published version: semver, branch/target/configuration, binaries, and the dependency list.
    /// This is the primary metadata shape used when resolving, downloading, or displaying a package version.
    /// </summary>
    public class PublishedPackageVersion : PublishedPackage
    {
        public PublishedPackageVersion() : base()
        {

        }

        public PublishedPackageVersion(PublishedPackage package) : base(package)
        {
        }

        public PublishedPackageVersion(PublishedPackageVersion packageVersion) : base(packageVersion)
        {
            PackageVersionId = packageVersion?.PackageVersionId;
            Version = packageVersion?.Version;
            Branch = packageVersion?.Branch;
            Target = packageVersion?.Target;
            Configuration = packageVersion?.Configuration;
            Compiled = packageVersion?.Compiled ?? 0;
            Released = packageVersion?.Released ?? 0;
            Notes = packageVersion?.Notes;
            PackageType = packageVersion?.PackageType;
            Binary = packageVersion?.Binary;
            BinaryDownloadUrl = packageVersion?.BinaryDownloadUrl;
            BinarySha256 = packageVersion?.BinarySha256;
            Dependencies = packageVersion?.Dependencies == null ? new List<PublishedPackageVersion>() : new List<PublishedPackageVersion>(packageVersion?.Dependencies);
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
        [JsonPropertyName("released")]
        public int Released { get; set; }
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
        [JsonPropertyName("binary")]
        public string Binary { get; set; }
        [JsonPropertyName("binary-download-url")]
        public string BinaryDownloadUrl { get; set; }
        [JsonPropertyName("binary-sha256")]
        public string BinarySha256 { get; set; }
        [JsonPropertyName("dependencies")]
        public List<PublishedPackageVersion> Dependencies { get; set; }
        [JsonPropertyName("type")]
        public string PackageType { get; set; }

        [JsonPropertyName("latest")]
        public PublishedPackageVersion Latest { get; set; }

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

        string _versionText;
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string VersionText
        {
            get
            {
                return _versionText == null ? Version : _versionText;
            }
            set
            {
                _versionText = value;
            }
        }

        public static bool operator ==(PublishedPackageVersion lhs, PublishedPackageVersion rhs)
        {
            return lhs?.Name == rhs?.Name && lhs?.Title == rhs?.Title && lhs?.Version == rhs?.Version && lhs?.Target == rhs?.Target && lhs?.Configuration == rhs?.Configuration && lhs?.Branch == rhs?.Branch;
        }

        public static bool operator !=(PublishedPackageVersion lhs, PublishedPackageVersion rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object o)
        {
            return this == o as PublishedPackageVersion;
        }
        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? new Random().Next();
        }

        public override string ToString()
        {
            return $"{Name} {Version} (distributor: {DistributorName}, branch: {Branch}, target: {Target}, configuration: {Configuration})";
        }
    }
}
