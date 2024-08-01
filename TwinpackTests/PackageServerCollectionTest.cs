using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class PackageServerCollectionTest
    {
        static PackageServerCollection _packageServers;
        static PackageServerMock _packageServer1;
        static PackageServerMock _packageServer2;
        static PackageServerMock _packageServerNotConnected;

        [ClassInitialize]
        public static void SetUp(TestContext context)
        {
            _packageServer1 = new PackageServerMock
            {
                CatalogItems = new List<CatalogItemGetResponse>
                {
                    new CatalogItemGetResponse() { Name = "Package 1" },
                    new CatalogItemGetResponse() { Name = "Package 2" },
                    new CatalogItemGetResponse() { Name = "Package 3" },
                    new CatalogItemGetResponse() { Name = "Package 4" },
                    new CatalogItemGetResponse() { Name = "Package 5" },
                },
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse() { Name = "Package 4", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                    new PackageVersionGetResponse() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                    new PackageVersionGetResponse() { Name = "Package 4", Version = "1.2.0.0", Branch = "main", Configuration = "Snapshot", Target = "TC3.1" },
                    new PackageVersionGetResponse() { Name = "Package 4", Version = "1.3.0.0", Branch = "main", Configuration = "Snapshot", Target = "TC3.1" },
                },
                Connected = true
            };

            _packageServer2 = new PackageServerMock
            {
                CatalogItems = new List<CatalogItemGetResponse>
                {
                    new CatalogItemGetResponse() { Name = "Package 4" },
                    new CatalogItemGetResponse() { Name = "Package 5" },
                    new CatalogItemGetResponse() { Name = "Package 6" },
                    new CatalogItemGetResponse() { Name = "Package 7" },
                    new CatalogItemGetResponse() { Name = "Package 8" },
                },
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse() { Name = "Package 4", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                    new PackageVersionGetResponse() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                },
                Connected = true
            };

            _packageServerNotConnected = new PackageServerMock
            {
                CatalogItems = new List<CatalogItemGetResponse>
                    {
                        new CatalogItemGetResponse() { Name = "Package 9" },
                        new CatalogItemGetResponse() { Name = "Package 10" },
                        new CatalogItemGetResponse() { Name = "Package 11" }
                    },
                Connected = false
            };

            _packageServers = new PackageServerCollection
            {
                _packageServer1,
                _packageServerNotConnected,
                _packageServer2,
            };
        }


        [TestMethod]
        public async Task SearchAllAsync_AllPackages()
        {
            var packages = new List<CatalogItem>();
            await foreach (var p in _packageServers.SearchAsync(null, maxPackages: null, batchSize: 2))
            {
                packages.Add(p);
            }
            Assert.AreEqual(8, packages.Count);
            Assert.AreEqual("Package 1", packages[0].Name);
            Assert.AreEqual("Package 2", packages[1].Name);
            Assert.AreEqual("Package 3", packages[2].Name);
            Assert.AreEqual("Package 4", packages[3].Name);
            Assert.AreEqual("Package 5", packages[4].Name);
            Assert.AreEqual("Package 6", packages[5].Name);
            Assert.AreEqual("Package 7", packages[6].Name);
            Assert.AreEqual("Package 8", packages[7].Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);
            Assert.AreEqual(_packageServer1, packages[4].PackageServer);
            Assert.AreEqual(_packageServer2, packages[5].PackageServer);
            Assert.AreEqual(_packageServer2, packages[6].PackageServer);
            Assert.AreEqual(_packageServer2, packages[7].PackageServer);
        }

        [TestMethod]
        public async Task SearchAllAsync_LimitedPackages4()
        {
            var packages = new List<CatalogItem>();
            await foreach (var p in _packageServers.SearchAsync(null, maxPackages: 4, batchSize: 2))
            {
                packages.Add(p);
            }
            Assert.AreEqual(4, packages.Count);
            Assert.AreEqual("Package 1", packages[0].Name);
            Assert.AreEqual("Package 2", packages[1].Name);
            Assert.AreEqual("Package 3", packages[2].Name);
            Assert.AreEqual("Package 4", packages[3].Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);
        }

        [TestMethod]
        public async Task SearchAllAsync_LimitedPackages6()
        {
            var packages = new List<CatalogItem>();
            await foreach (var p in _packageServers.SearchAsync(null, maxPackages: 6, batchSize: 2))
            {
                packages.Add(p);
            }
            Assert.AreEqual(6, packages.Count);
            Assert.AreEqual("Package 1", packages[0].Name);
            Assert.AreEqual("Package 2", packages[1].Name);
            Assert.AreEqual("Package 3", packages[2].Name);
            Assert.AreEqual("Package 4", packages[3].Name);
            Assert.AreEqual("Package 5", packages[4].Name);
            Assert.AreEqual("Package 6", packages[5].Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);
            Assert.AreEqual(_packageServer1, packages[4].PackageServer);
            Assert.AreEqual(_packageServer2, packages[5].PackageServer);
        }

        [TestMethod]
        public async Task SearchAllAsync_SearchTerm()
        {
            var packages = new List<CatalogItem>();
            await foreach (var p in _packageServers.SearchAsync("Package 4", maxPackages: 6, batchSize: 2))
            {
                packages.Add(p);
            }
            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual("Package 4", packages[0].Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
        }

        [DataTestMethod]
        [DataRow("1.1.0.0", "Package 4", "My Distributor", "1.0.0.0", "main", "Release", "TC3.1")]
        [DataRow("1.1.0.0", "Package 4", "My Distributor", "1.1.0.0", "main", "Release", "TC3.1")]
        [DataRow("1.3.0.0", "Package 4", "My Distributor", "1.2.0.0", "main", "Snapshot", "TC3.1")]
        public async Task ResolvePackageAsync_NormalLookUp_PackageServer1(string updateVersion, string name, string distributor, string version, string branch, string configuration, string target)
        {
            var package = new ConfigPlcPackage
            {
                Name = name,
                DistributorName = distributor,
                Version = version,
                Branch = branch,
                Configuration = configuration,
                Target = target
            };

            var catalogItem = await _packageServers.ResolvePackageAsync("plc1", package, null);

            Assert.AreEqual(package.Name, catalogItem.Name);
            Assert.AreEqual(package.Version, catalogItem.InstalledVersion);
            Assert.AreEqual(updateVersion, catalogItem.UpdateVersion);
            Assert.AreEqual(package, catalogItem.Config);
            Assert.AreEqual(false, catalogItem.IsPlaceholder);
            Assert.AreEqual(_packageServer1, catalogItem.PackageServer);
        }

        [DataTestMethod]
        [DataRow("1.0.0.0", "Package 5", "My Distributor", "1.0.0.0", "main", "Release", "TC3.1")]
        public async Task ResolvePackageAsync_NormalLookUp_PackageServer2(string updateVersion, string name, string distributor, string version, string branch, string configuration, string target)
        {
            var package = new ConfigPlcPackage
            {
                Name = name,
                DistributorName = distributor,
                Version = version,
                Branch = branch,
                Configuration = configuration,
                Target = target
            };

            var catalogItem = await _packageServers.ResolvePackageAsync("plc1", package, null);

            Assert.AreEqual(package.Name, catalogItem.Name);
            Assert.AreEqual(package.Version, catalogItem.InstalledVersion);
            Assert.AreEqual(updateVersion, catalogItem.UpdateVersion);
            Assert.AreEqual(package, catalogItem.Config);
            Assert.AreEqual(false, catalogItem.IsPlaceholder);
            Assert.AreEqual(_packageServer2, catalogItem.PackageServer);
        }


        [DataTestMethod]
        [DataRow("1.1.0.0", "Package 4", "My Distributor", "0.9.0.0", "main", "Release", "TC3.1")]
        public async Task ResolvePackageAsync_KnownButUnresolveable_PackageServer1(string updateVersion, string name, string distributor, string version, string branch, string configuration, string target)
        {
            var package = new ConfigPlcPackage
            {
                Name = name,
                DistributorName = distributor,
                Version = version,
                Branch = branch,
                Configuration = configuration,
                Target = target
            };

            var catalogItem = await _packageServers.ResolvePackageAsync("plc1", package, null);

            Assert.AreEqual(package.Name, catalogItem.Name);
            Assert.AreEqual(null, catalogItem.InstalledVersion);
            Assert.AreEqual(updateVersion, catalogItem.UpdateVersion);
            Assert.AreEqual(package, catalogItem.Config);
            Assert.AreEqual(false, catalogItem.IsPlaceholder);
            Assert.AreEqual(_packageServer1, catalogItem.PackageServer);
        }

        [DataTestMethod]
        [DataRow("1.0.0.0", "Package 5", "My Distributor", "0.9.0.0", "main", "Release", "TC3.1")]
        public async Task ResolvePackageAsync_KnownButUnresolveable_PackageServer2(string updateVersion, string name, string distributor, string version, string branch, string configuration, string target)
        {
            var package = new ConfigPlcPackage
            {
                Name = name,
                DistributorName = distributor,
                Version = version,
                Branch = branch,
                Configuration = configuration,
                Target = target
            };

            var catalogItem = await _packageServers.ResolvePackageAsync("plc1", package, null);

            Assert.AreEqual(package.Name, catalogItem.Name);
            Assert.AreEqual(null, catalogItem.InstalledVersion);
            Assert.AreEqual(updateVersion, catalogItem.UpdateVersion);
            Assert.AreEqual(package, catalogItem.Config);
            Assert.AreEqual(false, catalogItem.IsPlaceholder);
            Assert.AreEqual(_packageServer2, catalogItem.PackageServer);
        }

        [DataTestMethod]
        [DataRow(null, "Package 6", "My Distributor", "0.9.0.0", "main", "Release", "TC3.1")]
        public async Task ResolvePackageAsync_Unknown_PackageServer1(string updateVersion, string name, string distributor, string version, string branch, string configuration, string target)
        {
            var package = new ConfigPlcPackage
            {
                Name = name,
                DistributorName = distributor,
                Version = version,
                Branch = branch,
                Configuration = configuration,
                Target = target
            };

            var catalogItem = await _packageServers.ResolvePackageAsync("plc1", package, null);

            Assert.AreEqual(package.Name, catalogItem.Name);
            Assert.AreEqual(null, catalogItem.InstalledVersion);
            Assert.AreEqual(updateVersion, catalogItem.UpdateVersion);
            Assert.AreEqual(package, catalogItem.Config);
            Assert.AreEqual(false, catalogItem.IsPlaceholder);
            Assert.AreEqual(null, catalogItem.PackageServer);
        }
    }
}
