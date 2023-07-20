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
            InstalledVersion = packageVersion.Version;
            InstalledBranch = packageVersion.Branch;
            InstalledTarget = packageVersion.Target;
            InstalledConfiguration = packageVersion.Configuration;

            PackageId = packageVersion.PackageId;
            Repository = packageVersion.Repository;
            Description = packageVersion.Description;
            Entitlement = packageVersion.Entitlement;
            IconUrl = packageVersion.IconUrl;
            Name = packageVersion.Name;
            DisplayName = packageVersion.DisplayName;
            DistributorName = packageVersion.DistributorName;
        }

        public CatalogItem(ConfigPlcPackage package)
        {
            InstalledVersion = package.Version;
            InstalledBranch = package.Branch;
            InstalledTarget = package.Target;
            InstalledConfiguration = package.Configuration;

            Name = package.Name;
            Repository = package.Version;
            DistributorName = package.DistributorName;
            DisplayName = Name;
        }
        public string InstalledVersion { get; set; }
        public string UpdateVersion { get; set; }
        public string InstalledBranch { get; set; }
        public string InstalledTarget { get; set; }
        public string InstalledConfiguration { get; set; }
        public bool IsUpdateable { get { return InstalledVersion != null && UpdateVersion != null && new Version(InstalledVersion) < new Version(UpdateVersion); } }
    }
}
