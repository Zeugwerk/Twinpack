using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Twinpack.Configuration;

namespace Twinpack.Models
{
    /// <summary>
    /// Options stored on a PLC package reference (serialized with <see cref="PlcPackageReference"/>).
    /// </summary>
    public class PackageReferenceAddOptions
    {
        public PackageReferenceAddOptions()
        {
            LibraryReference = false;
            Optional = false;
            HideWhenReferencedAsDependency = false;
            PublishSymbolsInContainer = false;
            QualifiedOnly = false;
        }

        public PackageReferenceAddOptions(PackageReferenceAddOptions options)
        {
            if (options == null)
                return;

            LibraryReference = options.LibraryReference;
            Optional = options.Optional;
            HideWhenReferencedAsDependency = options.HideWhenReferencedAsDependency;
            PublishSymbolsInContainer = options.PublishSymbolsInContainer;
            QualifiedOnly = options.QualifiedOnly;
        }

        public PackageReferenceAddOptions CopyForDependency()
        {
            return new PackageReferenceAddOptions
            {
                LibraryReference = false,
                Optional = false,
                HideWhenReferencedAsDependency = false,
                PublishSymbolsInContainer = false,
                QualifiedOnly = this.QualifiedOnly,
            };
        }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("library-reference")]
        public bool LibraryReference { get; set; }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("hide")]
        public bool HideWhenReferencedAsDependency { get; set; }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("publish-all-symbols")]
        public bool PublishSymbolsInContainer { get; set; }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("qualified-only")]
        public bool QualifiedOnly { get; set; }
    }

    /// <summary>
    /// Minimal identity (name, distributor, optional version) passed into <see cref="Protocol.IPackageServer"/> to list or resolve versions.
    /// This is not persisted Twinpack config — use <see cref="PlcPackageReference"/> for stored references.
    /// </summary>
    public class PackageReferenceKey
    {
        public PackageReferenceKey() { }

        public PackageReferenceKey(PackageReferenceKey other)
        {
            Name = other?.Name;
            Version = other?.Version;
            DistributorName = other?.DistributorName;
            Options = other?.Options;
        }

        public string Name { get; set; }
        public string Version { get; set; }
        public string DistributorName { get; set; }
        public PackageReferenceAddOptions Options { get; set; }

        /// <summary>Builds a lookup key from persisted package configuration.</summary>
        public static PackageReferenceKey From(PlcPackageReference package)
        {
            if (package == null)
                return null;
            return new PackageReferenceKey
            {
                Name = package.Name,
                Version = package.Version,
                DistributorName = package.DistributorName,
                Options = package.Options
            };
        }

        public static bool operator ==(PackageReferenceKey lhs, PackageReferenceKey rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return true;
            if (lhs is null || rhs is null)
                return false;
            return lhs.Name == rhs.Name && lhs.Version == rhs.Version && lhs.DistributorName == rhs.DistributorName;
        }

        public static bool operator !=(PackageReferenceKey lhs, PackageReferenceKey rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object o)
        {
            return this == o as PackageReferenceKey;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + (Name?.GetHashCode() ?? 0);
                hash = hash * 23 + (Version?.GetHashCode() ?? 0);
                hash = hash * 23 + (DistributorName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
