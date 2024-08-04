using System;
using System.ComponentModel;
using Twinpack.Models.Api;

namespace Twinpack.Models
{
    public class CatalogItem : CatalogItemGetResponse, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        PackageGetResponse _package = new PackageGetResponse();
        PackageVersionGetResponse _packageVersion = new PackageVersionGetResponse();

        public CatalogItem()
        {

        }

        public CatalogItem(Protocol.IPackageServer packageServer, CatalogItemGetResponse package) : base(package)
        {
            PackageServer = packageServer;
        }

        public CatalogItem(Protocol.IPackageServer packageServer, PackageVersionGetResponse packageVersion)
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

        public CatalogItem(ConfigPlcPackage package)
        {
            Name = package.Name;
            Repository = package.Version;
            DistributorName = package.DistributorName;
            DisplayName = Name;
            Config = package;

            IsPlaceholder = package.Version == null;
        }

        public Protocol.IPackageServer PackageServer { get; set; }
        public string InstalledVersion { get { return Installed?.Version; } }
        public bool IsPlaceholder { get; set; } // this may be null if the latest version should be used
        public string InstalledBranch { get { return Installed?.Branch; } }
        public string InstalledTarget { get { return Installed?.Target; } }
        public string InstalledConfiguration { get { return Installed?.Configuration; } }
        public PackageVersionGetResponse Update{ get; set; }
        public PackageVersionGetResponse Installed { get; set; }
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
                    return (Installed?.Version == null && Update?.Version != null) || (Update?.Version != null && new Version(Installed?.Version) < new Version(Update?.Version));
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
