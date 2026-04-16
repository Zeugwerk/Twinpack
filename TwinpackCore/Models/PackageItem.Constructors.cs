using Twinpack.Configuration;
using Twinpack.Protocol.Api;

namespace Twinpack.Models
{
    public partial class PackageItem
    {
        public PackageItem()
        {

        }

        public PackageItem(PackageItem p)
        {
            PackageServer = p.PackageServer;

            var catalog = new CatalogPackageSummary
            {
                PackageId = p.Catalog?.PackageId,
                Repository = p.Catalog?.Repository,
                Description = p.Catalog?.Description,
                IconUrl = p.Catalog?.IconUrl,
                Name = p.Catalog?.Name,
                DisplayName = p.Catalog?.DisplayName,
                DistributorName = p.Catalog?.DistributorName,
                RuntimeLicense = p.Catalog?.RuntimeLicense,
                Downloads = p.Catalog?.Downloads,
            };

            Catalog = catalog;
            Used = p.Used;
            PlcPackageReference = p.PlcPackageReference;
            Package = p.Package;
            PackageVersion = p.PackageVersion;
            ProjectName = p.ProjectName;
            PlcName = p.PlcName;
        }

        public PackageItem(Protocol.IPackageServer packageServer, CatalogPackageSummary package)
        {
            Catalog = package;
            PackageServer = packageServer;
        }

        public PackageItem(Protocol.IPackageServer packageServer, PublishedPackageVersion packageVersion)
        {
            PackageServer = packageServer;

            var catalog = new CatalogPackageSummary
            {
                PackageId = packageVersion?.PackageId,
                Repository = packageVersion?.Repository,
                Description = packageVersion?.Description,
                IconUrl = packageVersion?.IconUrl,
                Name = packageVersion?.Name,
                DisplayName = packageVersion?.DisplayName,
                DistributorName = packageVersion?.DistributorName,
                RuntimeLicense = packageVersion?.LicenseTmcBinary != null ? 1 : 0,
                Downloads = packageVersion?.Downloads
            };

            Catalog = catalog;
        }

        public PackageItem(PlcPackageReference package)
        {
            var catalog = new CatalogPackageSummary
            {
                Name = package?.Name,
                Repository = package?.Version,
                DistributorName = package?.DistributorName,
                DisplayName = package?.Name,
            };

            Catalog = catalog;
            PlcPackageReference = package;
        }
    }
}
