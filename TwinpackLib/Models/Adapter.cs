using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Models
{
    public class CatalogItem : CatalogItemGetResponse
    {
        public CatalogItem()
        {

        }

        public CatalogItem(CatalogItemGetResponse package) : base(package)
        {
            
        }

        public CatalogItem(PackageVersionGetResponse packageVersion)
        {
            PackageId = packageVersion.PackageId;
            Repository = packageVersion.Repository;
            Description = packageVersion.Description;
            IconUrl = packageVersion.IconUrl;
            Name = packageVersion.Name;
            DisplayName = packageVersion.DisplayName;
            DistributorName = packageVersion.DistributorName;
            RuntimeLicense = packageVersion.LicenseTmcBinary != null ? 1 : 0;
        }

        public CatalogItem(ConfigPlcPackage package)
        {
            Name = package.Name;
            Repository = package.Version;
            DistributorName = package.DistributorName;
            DisplayName = Name;
        }
        public string InstalledVersion { get { return Installed?.Version; } }
        public string InstalledBranch { get { return Installed?.Branch; } }
        public string InstalledTarget { get { return Installed?.Target; } }
        public string InstalledConfiguration { get { return Installed?.Configuration; } }
        public PackageVersionGetResponse Update { get; set; }
        public PackageVersionGetResponse Installed { get; set; }

        public bool IsUpdateable 
        { 
            get
            {
                try
                {
                    return InstalledVersion != null && Update?.Version != null && new Version(InstalledVersion) < new Version(Update?.Version);
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
