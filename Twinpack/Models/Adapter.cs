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

        public CatalogItem(ConfigPlcPackage package)
        {
            InstalledVersion = package.Version;
            InstalledBranch = package.Branch;
            InstalledTarget = package.Target;
            InstalledConfiguration = package.Configuration;

            Repository = package.Version;
            Name = package.Name;
            DistributorName = package.DistributorName;
        }
        public string InstalledVersion { get; set; }
        public string UpdateVersion { get; set; }
        public string InstalledBranch { get; set; }
        public string InstalledTarget { get; set; }
        public string InstalledConfiguration { get; set; }
    }
}
