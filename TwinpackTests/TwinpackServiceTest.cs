using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;

namespace TwinpackTests
{
    [TestClass]
    public class TwinpackServiceTest
    {
        static PackageServerCollection _packageServers;
        static PackageServerMock _packageServer1;
        static PackageServerMock _packageServer2;
        static PackageServerMock _packageServerNotConnected;
        static TwinpackService _twinpack;

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

            _twinpack = new TwinpackService(_packageServers);
        }


        [TestMethod]
        public async Task RetrieveAvailablePackagesAsync_AllPackages()
        {
            var packages = (await _twinpack.RetrieveAvailablePackagesAsync()).ToList();

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

            CollectionAssert.AreEqual(packages.ToList().AsReadOnly(), packages.ToList());
            Assert.AreEqual(false, _twinpack.HasMoreAvailablePackages);
        }

        [TestMethod]
        public async Task RetrieveAvailablePackagesAsync_LoadMorePackages()
        {
            var packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();

            Assert.AreEqual(4, packages.Count);
            Assert.AreEqual("Package 1", packages[0].Name);
            Assert.AreEqual("Package 2", packages[1].Name);
            Assert.AreEqual("Package 3", packages[2].Name);
            Assert.AreEqual("Package 4", packages[3].Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);

            Assert.AreEqual(true, _twinpack.HasMoreAvailablePackages);


            packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 5)).ToList();

            Assert.AreEqual(false, _twinpack.HasMoreAvailablePackages);

            packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();

            Assert.AreEqual(8, packages.Count);

            Assert.AreEqual("Package 5", packages[4].Name);
            Assert.AreEqual("Package 6", packages[5].Name);
            Assert.AreEqual("Package 7", packages[6].Name);
            Assert.AreEqual("Package 8", packages[7].Name);

            Assert.AreEqual(_packageServer1, packages[4].PackageServer);
            Assert.AreEqual(_packageServer2, packages[5].PackageServer);
            Assert.AreEqual(_packageServer2, packages[6].PackageServer);
            Assert.AreEqual(_packageServer2, packages[7].PackageServer);

            packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(8, packages.Count);
        }

        [TestMethod]
        public async Task RetrieveAvailablePackagesAsync_LoadMorePackages_WithSearchTerm()
        {
            var packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: "Package 5", maxNewPackages: 4)).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual("Package 5", packages[0].Name);
            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(false, _twinpack.HasMoreAvailablePackages);

            packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();

            Assert.AreEqual(5, packages.Count);

            Assert.AreEqual("Package 5", packages[0].Name);
            Assert.AreEqual("Package 1", packages[1].Name);
            Assert.AreEqual("Package 2", packages[2].Name);
            Assert.AreEqual("Package 3", packages[3].Name);
            Assert.AreEqual("Package 4", packages[4].Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);
            Assert.AreEqual(_packageServer1, packages[4].PackageServer);

            Assert.AreEqual(true, _twinpack.HasMoreAvailablePackages);

            packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(8, packages.Count);
            Assert.AreEqual(false, _twinpack.HasMoreAvailablePackages);

            packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(8, packages.Count);
            Assert.AreEqual(false, _twinpack.HasMoreAvailablePackages);
        }

        [TestMethod]
        public async Task InvalidateCacheAsync_AvailablePackagesIsReset()
        {
            var packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(4, packages.Count);

            _twinpack.InvalidateCache();

            packages = (await _twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(4, packages.Count);
        }
    }
}
