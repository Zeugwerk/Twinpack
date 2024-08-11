using System;
using System.ComponentModel;
using Twinpack.Configuration;
using Twinpack.Protocol.Api;

namespace Twinpack.Models
{
    public class PackageItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        CatalogItemGetResponse _catalog;
        PackageGetResponse _package;
        PackageVersionGetResponse _packageVersion;

        public PackageItem()
        {

        }

        public PackageItem(PackageItem p)
        {
            PackageServer = p.PackageServer;

            var catalog = new CatalogItemGetResponse
            {
                PackageId = p.Catalog?.PackageId,
                Repository = p.Catalog?.Repository,
                Description = p.Catalog?.Description,
                IconUrl = p.Catalog?.IconUrl,
                Name = p.Catalog?.Name,
                DisplayName = p.Catalog?.DisplayName,
                DistributorName = p.Catalog?.DistributorName,
                RuntimeLicense = p.Catalog.RuntimeLicense,
                Downloads = p.Catalog?.Downloads,
            };

            Catalog = catalog;
            Used = p.Used;
            Config = p.Config;
            Package = p.Package;
            PackageVersion = p.PackageVersion;
            ProjectName = p.ProjectName;
            PlcName = p.PlcName;
            IsPlaceholder = p.IsPlaceholder;
        }

        public PackageItem(Protocol.IPackageServer packageServer, CatalogItemGetResponse package)
        {
            Catalog = package;
            PackageServer = packageServer;
        }

        public PackageItem(Protocol.IPackageServer packageServer, PackageVersionGetResponse packageVersion)
        {
            PackageServer = packageServer;

            var catalog = new CatalogItemGetResponse
            {
                PackageId = packageVersion.PackageId,
                Repository = packageVersion.Repository,
                Description = packageVersion.Description,
                IconUrl = packageVersion.IconUrl,
                Name = packageVersion.Name,
                DisplayName = packageVersion.DisplayName,
                DistributorName = packageVersion.DistributorName,
                RuntimeLicense = packageVersion.LicenseTmcBinary != null ? 1 : 0,
                Downloads = packageVersion.Downloads
            };

            Catalog = catalog;
        }

        public PackageItem(ConfigPlcPackage package)
        {
            var catalog = new CatalogItemGetResponse
            {
                Name = package.Name,
                Repository = package.Version,
                DistributorName = package.DistributorName,
                DisplayName = package.Name,
            };

            Catalog = catalog;
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

        public CatalogItemGetResponse Catalog
        {
            get { return _catalog; }
            set
            {
                _catalog = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Catalog)));
            }
        }

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
