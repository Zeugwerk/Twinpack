using System;
using System.ComponentModel;
using Twinpack.Models.Api;

namespace Twinpack.Models
{
    public class PackageItem : CatalogItemGetResponse, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        PackageGetResponse _package;
        PackageVersionGetResponse _packageVersion;

        public PackageItem()
        {

        }

        public PackageItem(PackageItem p)
        {
            PackageServer = p.PackageServer;
            PackageId = p.PackageId;
            Repository = p.Repository;
            Description = p.Description;
            IconUrl = p.IconUrl;
            Name = p.Name;
            DisplayName = p.DisplayName;
            DistributorName = p.DistributorName;
            RuntimeLicense = p.RuntimeLicense;
            Downloads = p.Downloads;
            Used = p.Used;
            Config = p.Config;
            Package = p.Package;
            PackageVersion = p.PackageVersion;
            ProjectName = p.ProjectName;
            PlcName = p.PlcName;
            IsPlaceholder = p.IsPlaceholder;
        }

        public PackageItem(Protocol.IPackageServer packageServer, CatalogItemGetResponse package) : base(package)
        {
            PackageServer = packageServer;
        }

        public PackageItem(Protocol.IPackageServer packageServer, PackageVersionGetResponse packageVersion)
        {
            PackageServer = packageServer;
            PackageId = packageVersion.PackageId;
            Repository = packageVersion.Repository;
            Description = packageVersion.Description;
            IconUrl = packageVersion.IconUrl;
            Name = packageVersion.Name;
            DisplayName = packageVersion.DisplayName;
            DistributorName = packageVersion.DistributorName;
            RuntimeLicense = packageVersion.LicenseTmcBinary != null ? 1 : 0;
            Downloads = packageVersion.Downloads;
        }

        public PackageItem(ConfigPlcPackage package)
        {
            Name = package.Name;
            Repository = package.Version;
            DistributorName = package.DistributorName;
            DisplayName = Name;
            Config = package;

            IsPlaceholder = package.Version == null;
        }

        public Protocol.IPackageServer PackageServer { get; set; }
        public string InstalledVersion { get { return Used?.Version; } }
        public bool IsPlaceholder { get; set; } // this may be null if the latest version should be used
        public string InstalledBranch { get { return Used?.Branch; } }
        public string InstalledTarget { get { return Used?.Target; } }
        public string InstalledConfiguration { get { return Used?.Configuration; } }
        public PackageVersionGetResponse Update{ get; set; }
        public PackageVersionGetResponse Used { get; set; }
        public string ProjectName { get; set; }
        public string PlcName { get; set; }
        public ConfigPlcPackage Config { get; set; }
        public PackageGetResponse Package
        {
            get { return _package; }
            set
            {
                _package = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Package)));
            }
        }
        public PackageVersionGetResponse PackageVersion
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
            _package = new PackageGetResponse();
            _packageVersion = new PackageVersionGetResponse();
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
