using System;
using System.ComponentModel;
using Twinpack.Configuration;
using Twinpack.Protocol.Api;

namespace Twinpack.Models
{
    /// <summary>
    /// Mutable row combining PLC placement (<see cref="ProjectName"/>, <see cref="PlcName"/>),
    /// persisted <see cref="PlcPackageReference"/>, catalog/package/version payloads from servers, and optional UI binding.
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
        public void Invalidate()
        {
            Package = null;
            PackageVersion = null;
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
