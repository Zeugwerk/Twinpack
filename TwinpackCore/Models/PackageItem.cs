using System;
using System.ComponentModel;
using Twinpack.Configuration;
using Twinpack.Protocol.Api;

namespace Twinpack.Models
{
    /// <summary>
    /// Mutable row combining PLC placement (<see cref="ProjectName"/>, <see cref="PlcName"/>),
    /// persisted <see cref="PlcPackageReference"/>, catalog/package/version payloads from servers, and optional UI binding.
    /// Use <see cref="GetConfiguredPackageRef"/>, <see cref="GetResolvedPackageRef"/>, and <see cref="GetInstalledPackageRef"/> for explicit
    /// <see cref="ConfiguredPackageRef"/> / <see cref="ResolvedPackageRef"/> / <see cref="InstalledPackageRef"/> views.
    /// </summary>
    public partial class PackageItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        CatalogPackageSummary _catalog;
        PublishedPackage _package;
        PublishedPackageVersion _packageVersion;

        public Protocol.IPackageServer PackageServer { get; set; }
        public string InstalledVersion { get { return Used?.Version; } }
        public bool IsPlaceholder { get => Used != null && PlcPackageReference?.Version == null; }
        public string InstalledBranch { get { return Used?.Branch; } }
        public string InstalledTarget { get { return Used?.Target; } }
        public string InstalledConfiguration { get { return Used?.Configuration; } }
        public PublishedPackageVersion Update{ get; set; }
        public PublishedPackageVersion Used { get; set; }
        public string ProjectName { get; set; }
        public string PlcName { get; set; }
        public PlcPackageReference PlcPackageReference { get; set; }

        public CatalogPackageSummary Catalog
        {
            get { return _catalog; }
            set
            {
                _catalog = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catalog)));
            }
        }

        public PublishedPackage Package
        {
            get { return _package; }
            set
            {
                _package = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
            }
        }
        public PublishedPackageVersion PackageVersion
        {
            get { return _packageVersion; }
            set
            {
                _packageVersion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageVersion)));
            }
        }
        /// <summary>Configured intent: solution placement + persisted <see cref="PlcPackageReference"/>.</summary>
        public ConfiguredPackageRef? GetConfiguredPackageRef()
        {
            if (PlcPackageReference == null && string.IsNullOrEmpty(ProjectName) && string.IsNullOrEmpty(PlcName))
                return null;
            return new ConfiguredPackageRef(ProjectName, PlcName, PlcPackageReference);
        }

        public void Apply(ConfiguredPackageRef configured)
        {
            if (configured == null)
                return;
            ProjectName = configured.ProjectName;
            PlcName = configured.PlcName;
            PlcPackageReference = configured.Reference;
        }

        /// <summary>Server resolution used for download/install in this flow (<see cref="PackageVersion"/> / <see cref="Package"/>).</summary>
        public ResolvedPackageRef? GetResolvedPackageRef()
        {
            if (PackageVersion == null)
                return null;
            return new ResolvedPackageRef(PackageVersion, Package);
        }

        public void Apply(ResolvedPackageRef? resolved)
        {
            if (resolved == null)
            {
                Package = null;
                PackageVersion = null;
                return;
            }
            Package = resolved.Package;
            PackageVersion = resolved.Version;
        }

        /// <summary>Automation view of what is installed / effective for placeholders (<see cref="Used"/>).</summary>
        public InstalledPackageRef? GetInstalledPackageRef()
        {
            if (Used == null)
                return null;
            return new InstalledPackageRef(Used);
        }

        public void Apply(InstalledPackageRef? installed)
        {
            Used = installed?.Version;
        }

        public void Invalidate()
        {
            Apply((ResolvedPackageRef?)null);
        }

        public bool IsUpdateable
        {
            get
            {
                try
                {
                    return (Used?.Version == null && Update?.Version != null) || (Update?.Version != null && new Version(Used?.Version) < new Version(Update?.Version));
                }
                catch
                {
                    return true;
                }
            }
        }
        public string UpdateVersion { get { return Update?.Version; } }

    }
}
