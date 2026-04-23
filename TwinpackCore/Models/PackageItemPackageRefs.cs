using Twinpack.Configuration;
using Twinpack.Protocol.Api;

namespace Twinpack.Models
{
    /// <summary>
    /// Placement plus the persisted Twinpack reference (what <c>config.json</c> expresses: optional version, branch, options).
    /// </summary>
    public sealed class ConfiguredPackageRef
    {
        public ConfiguredPackageRef(string? projectName, string? plcName, PlcPackageReference? reference)
        {
            ProjectName = projectName;
            PlcName = plcName;
            Reference = reference;
        }

        public string? ProjectName { get; }
        public string? PlcName { get; }
        public PlcPackageReference? Reference { get; }
    }

    /// <summary>
    /// Registry resolution for the current operation: concrete version metadata and optional package-level payload.
    /// </summary>
    public sealed class ResolvedPackageRef
    {
        public ResolvedPackageRef(PublishedPackageVersion version, PublishedPackage? package = null)
        {
            Version = version ?? throw new System.ArgumentNullException(nameof(version));
            Package = package;
        }

        public PublishedPackageVersion Version { get; }
        public PublishedPackage? Package { get; }
    }

    /// <summary>
    /// Effective installation / placeholder resolution from automation (what is actually on the PLC for this reference).
    /// </summary>
    public sealed class InstalledPackageRef
    {
        public InstalledPackageRef(PublishedPackageVersion version)
        {
            Version = version ?? throw new System.ArgumentNullException(nameof(version));
        }

        public PublishedPackageVersion Version { get; }
    }
}
