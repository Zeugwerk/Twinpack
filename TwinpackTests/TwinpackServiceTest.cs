using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    [TestClass]
    public class TwinpackServiceTest
    {
        PackageServerCollection _packageServers;
        PackageServerMock _packageServer1;
        PackageServerMock _packageServer2;
        PackageServerMock _packageServerNotConnected;

        [TestInitialize]
        public void TestInitialize()
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
                    new PackageVersionGetResponse() { Name = "Package 4", DistributorName = "My Distributor 4", DisplayName="My Displayname", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                    new PackageVersionGetResponse() { Name = "Package 4", DistributorName = "My Distributor 4", DisplayName="My Displayname", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                    new PackageVersionGetResponse() { Name = "Package 4", DistributorName = "My Distributor 4", DisplayName="My Displayname", Version = "1.2.0.0", Branch = "main", Configuration = "Snapshot", Target = "TC3.1" },
                    new PackageVersionGetResponse() { Name = "Package 4", DistributorName = "My Distributor 4", DisplayName="My Displayname", Version = "1.3.0.0", Branch = "main", Configuration = "Snapshot", Target = "TC3.1" },
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
                    new PackageVersionGetResponse() { Name = "Package 5", DistributorName = "My Distributor 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
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
        public async Task RetrieveAvailablePackagesAsync_AllPackages()
        {
            var twinpack = new TwinpackService(_packageServers);

            var packages = (await twinpack.RetrieveAvailablePackagesAsync()).ToList();

            Assert.AreEqual(8, packages.Count);
            Assert.AreEqual("Package 1", packages[0].Catalog?.Name);
            Assert.AreEqual("Package 2", packages[1].Catalog?.Name);
            Assert.AreEqual("Package 3", packages[2].Catalog?.Name);
            Assert.AreEqual("Package 4", packages[3].Catalog?.Name);
            Assert.AreEqual("Package 5", packages[4].Catalog?.Name);
            Assert.AreEqual("Package 6", packages[5].Catalog?.Name);
            Assert.AreEqual("Package 7", packages[6].Catalog?.Name);
            Assert.AreEqual("Package 8", packages[7].Catalog?.Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);
            Assert.AreEqual(_packageServer1, packages[4].PackageServer);
            Assert.AreEqual(_packageServer2, packages[5].PackageServer);
            Assert.AreEqual(_packageServer2, packages[6].PackageServer);
            Assert.AreEqual(_packageServer2, packages[7].PackageServer);

            CollectionAssert.AreEqual(packages.ToList().AsReadOnly(), packages.ToList());
            Assert.AreEqual(false, twinpack.HasMoreAvailablePackages);
        }

        [TestMethod]
        public async Task RetrieveAvailablePackagesAsync_LoadMorePackages()
        {
            var twinpack = new TwinpackService(_packageServers);

            var packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();

            Assert.AreEqual(4, packages.Count);
            Assert.AreEqual("Package 1", packages[0].Catalog?.Name);
            Assert.AreEqual("Package 2", packages[1].Catalog?.Name);
            Assert.AreEqual("Package 3", packages[2].Catalog?.Name);
            Assert.AreEqual("Package 4", packages[3].Catalog?.Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);

            Assert.AreEqual(true, twinpack.HasMoreAvailablePackages);


            packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 5)).ToList();

            Assert.AreEqual(false, twinpack.HasMoreAvailablePackages);

            packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();

            Assert.AreEqual(8, packages.Count);

            Assert.AreEqual("Package 5", packages[4].Catalog?.Name);
            Assert.AreEqual("Package 6", packages[5].Catalog?.Name);
            Assert.AreEqual("Package 7", packages[6].Catalog?.Name);
            Assert.AreEqual("Package 8", packages[7].Catalog?.Name);

            Assert.AreEqual(_packageServer1, packages[4].PackageServer);
            Assert.AreEqual(_packageServer2, packages[5].PackageServer);
            Assert.AreEqual(_packageServer2, packages[6].PackageServer);
            Assert.AreEqual(_packageServer2, packages[7].PackageServer);

            packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(8, packages.Count);
        }

        [TestMethod]
        public async Task RetrieveAvailablePackagesAsync_LoadMorePackages_WithSearchTerm()
        {
            var twinpack = new TwinpackService(_packageServers);

            var packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: "Package 5", maxNewPackages: 4)).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual("Package 5", packages[0].Catalog?.Name);
            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(false, twinpack.HasMoreAvailablePackages);

            packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();

            Assert.AreEqual(5, packages.Count);

            Assert.AreEqual("Package 5", packages[0].Catalog?.Name);
            Assert.AreEqual("Package 1", packages[1].Catalog?.Name);
            Assert.AreEqual("Package 2", packages[2].Catalog?.Name);
            Assert.AreEqual("Package 3", packages[3].Catalog?.Name);
            Assert.AreEqual("Package 4", packages[4].Catalog?.Name);

            Assert.AreEqual(_packageServer1, packages[0].PackageServer);
            Assert.AreEqual(_packageServer1, packages[1].PackageServer);
            Assert.AreEqual(_packageServer1, packages[2].PackageServer);
            Assert.AreEqual(_packageServer1, packages[3].PackageServer);
            Assert.AreEqual(_packageServer1, packages[4].PackageServer);

            Assert.AreEqual(true, twinpack.HasMoreAvailablePackages);

            packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(8, packages.Count);
            Assert.AreEqual(false, twinpack.HasMoreAvailablePackages);

            packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(8, packages.Count);
            Assert.AreEqual(false, twinpack.HasMoreAvailablePackages);
        }

        [TestMethod]
        public async Task InvalidateCacheAsync_AvailablePackagesIsReset()
        {
            var twinpack = new TwinpackService(_packageServers);

            var packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(4, packages.Count);

            twinpack.InvalidateCache();

            packages = (await twinpack.RetrieveAvailablePackagesAsync(searchTerm: null, maxNewPackages: 4)).ToList();
            Assert.AreEqual(4, packages.Count);
        }

        [TestMethod]
        public async Task RetrieveUsedPackagesAsync_WithoutSearchTerm()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };

            var twinpack = new TwinpackService(_packageServers, config: config);

            var packages = (await twinpack.RetrieveUsedPackagesAsync(searchTerm: null)).ToList();

            Assert.AreEqual(3, packages.Count);
            Assert.AreEqual(2, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(1, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveUsedPackagesAsync_LinkToConfig()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Name = "MyProject", Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { Name = "MyPlc" } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };
            var twinpack = new TwinpackService(_packageServers, config: config);

            var packages = (await twinpack.RetrieveUsedPackagesAsync(searchTerm: null)).ToList();

            Assert.AreEqual(3, packages.Count);
            Assert.AreEqual(2, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(1, packages.Where(x => x.Used == null).Count());
            CollectionAssert.AreEqual(new List<string> { "MyProject" }, packages.Select(x => x.ProjectName).Distinct().ToList());
            CollectionAssert.AreEqual(new List<string> { "MyPlc" }, packages.Select(x => x.PlcName).Distinct().ToList());
        }

        [TestMethod]
        public async Task RetrieveUsedPackagesAsync_WithSearchTerm_PackageName()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };
            var twinpack = new TwinpackService(_packageServers, config: config);

            var packages = (await twinpack.RetrieveUsedPackagesAsync(searchTerm: "4")).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(1, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(0, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveUsedPackagesAsync_WithSearchTerm_DistributorName()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };
            var twinpack = new TwinpackService(_packageServers, config: config);

            var packages = (await twinpack.RetrieveUsedPackagesAsync(searchTerm: "My Distributor 5")).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(1, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(0, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveUsedPackagesAsync_WithSearchTerm_DisplayName()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };

            var twinpack = new TwinpackService(_packageServers, config: config);

            var packages = (await twinpack.RetrieveUsedPackagesAsync(searchTerm: "My Displayname")).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(1, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(0, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveUsedPackagesAsync_ResolvingNeeded()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = null, Branch = null, Target = null, Configuration = null },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = null, Target = null, Configuration = null },
                new ConfigPlcPackage() { Name = "Package 6", Version = "0.0.0.0", Branch = null, Target = null, Configuration = null },
            };
            var twinpack = new TwinpackService(_packageServers, config: config);

            var packages = (await twinpack.RetrieveUsedPackagesAsync(searchTerm: null)).ToList();

            Assert.AreEqual(3, packages.Count);
            Assert.AreEqual(2, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(1, packages.Where(x => x.Used == null).Count());

            Assert.AreEqual("My Distributor 4", packages[0].Config.DistributorName);
            Assert.AreEqual("main", packages[0].Config.Branch);
            Assert.AreEqual("Snapshot", packages[0].Config.Configuration);
            Assert.AreEqual("TC3.1", packages[0].Config.Target);
            Assert.AreEqual(null, packages[0].Config.Version);
            Assert.AreEqual("1.3.0.0", packages[0].Used.Version);

            Assert.AreEqual("My Distributor 5", packages[1].Config.DistributorName);
            Assert.AreEqual("main", packages[1].Config.Branch);
            Assert.AreEqual("Release", packages[1].Config.Configuration);
            Assert.AreEqual("TC3.1", packages[1].Config.Target);
            Assert.AreEqual("1.0.0.0", packages[1].Config.Version);
            Assert.AreEqual("1.0.0.0", packages[1].Used.Version);

            Assert.AreEqual(null, packages[2].Config.DistributorName);
            Assert.AreEqual(null, packages[2].Config.Branch);
            Assert.AreEqual(null, packages[2].Config.Configuration);
            Assert.AreEqual(null, packages[2].Config.Target);
            Assert.AreEqual("0.0.0.0", packages[2].Config.Version);
            Assert.AreEqual(null, packages[2].Used);
        }

        [DataTestMethod]
        [DataRow(null, null, 6)]
        [DataRow("MyProject1", "MyPlc1", 1)]
        [DataRow("MyProject2", "MyPlc2", 1)]
        [DataRow("MyProject3", "MyPlc3", 0)]
        [DataRow(null, "MyPlc1", 3)]
        public async Task RetrieveUsedPackagesAsync_ProjectAndPlcFilter(string projectName, string plcName, int expectedPackageCount)
        {
            var config = new Config { Projects = new List<ConfigProject> {
                new ConfigProject { Name = "MyProject1", Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { Name = "MyPlc1" }, new ConfigPlcProject { Name = "MyPlc2" } } } ,
                new ConfigProject { Name = "MyProject2", Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { Name = "MyPlc1" }, new ConfigPlcProject { Name = "MyPlc2" } } } ,
                new ConfigProject { Name = "MyProject3", Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { Name = "MyPlc1" }, new ConfigPlcProject { Name = "MyPlc2" } } } ,
                }
            };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage> { new ConfigPlcPackage() { Name = "Package 4", Version = "1.0.0.0" } };
            config.Projects[0].Plcs[1].Packages = new List<ConfigPlcPackage> { new ConfigPlcPackage() { Name = "Package 4", Version = "1.0.0.0" } };
            config.Projects[1].Plcs[0].Packages = new List<ConfigPlcPackage> { new ConfigPlcPackage() { Name = "Package 4", Version = "1.0.0.0" } };
            config.Projects[1].Plcs[1].Packages = new List<ConfigPlcPackage> { new ConfigPlcPackage() { Name = "Package 4", Version = "1.0.0.0" } };
            config.Projects[2].Plcs[0].Packages = new List<ConfigPlcPackage> { new ConfigPlcPackage() { Name = "Package 4", Version = "1.0.0.0" } };
            config.Projects[2].Plcs[1].Packages = new List<ConfigPlcPackage> { new ConfigPlcPackage() { Name = "Package 4", Version = "1.0.0.0" } };

            var twinpack = new TwinpackService(_packageServers, config: config, projectName: projectName, plcName: plcName);
            var packages = (await twinpack.RetrieveUsedPackagesAsync(searchTerm: null)).ToList();

            Assert.AreEqual(expectedPackageCount, packages.Count);
        }

        [TestMethod]
        public async Task FetchPackageAsync_OnlyWithCatalog()
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore" },
                            new PackageVersionGetResponse() { Name = "ZPlatform" },
                        }
                    }
                },
                Connected = true
            };

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers);

            var package = new PackageItem { Catalog = new CatalogItemGetResponse { Name = "ZAux" } };

            await twinpack.FetchPackageAsync(package);

            Assert.AreEqual("ZAux", package.Config.Name);
            Assert.AreEqual("ZAux", package.Package.Name);
            Assert.AreEqual("ZAux", package.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", package.PackageVersion.Version);
            Assert.IsTrue(package.Package.Branches.Contains("main"));
            Assert.IsTrue(package.Package.Branches.Contains("release/1.0"));
        }

        [TestMethod]
        public async Task FetchPackageAsync_PackageMetadataIsPopulated()
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore" },
                            new PackageVersionGetResponse() { Name = "ZPlatform" },
                        }
                    }
                },
                Connected = true
            };

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers);

            var package = new PackageItem { Config = new ConfigPlcPackage { Name = "ZAux" } };

            await twinpack.FetchPackageAsync(package);

            Assert.AreEqual("ZAux", package.Package.Name);
            Assert.AreEqual("ZAux", package.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", package.PackageVersion.Version);
            Assert.IsTrue(package.Package.Branches.Contains("main"));
            Assert.IsTrue(package.Package.Branches.Contains("release/1.0"));
        }

        [DataTestMethod]
        [DataRow("2.0.0.0", "1.0.0.0")]
        [DataRow("1.0.0.0", "1.0.0.0")]
        public async Task FetchPackageAsync_EffectiveVersion_WithCatalog(string effectiveVersion, string expectedPackageVersion)
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux",
                        Title = "ZAux",
                        Version = "1.0.0.0",
                        DistributorName = "My Company",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1"
                    }
                },
                Connected = true
            };

            var automationInterfaceMock = new Mock<IAutomationInterface>();
            automationInterfaceMock.Setup(x => x.ResolveEffectiveVersionAsync("TestProject1", "Untitled1", "ZAux")).Returns(Task.Run(() => effectiveVersion));

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers, automationInterfaceMock.Object);

            var package = new PackageItem { ProjectName = "TestProject1", PlcName = "Untitled1", Catalog = new CatalogItemGetResponse { Name = "ZAux" } };

            await twinpack.FetchPackageAsync(package);

            Assert.AreEqual("TestProject1", package.ProjectName);
            Assert.AreEqual("Untitled1", package.PlcName);
            Assert.AreEqual("ZAux", package.Config.Name);
            Assert.AreEqual("ZAux", package.Package.Name);
            Assert.AreEqual("ZAux", package.PackageVersion.Name);
            Assert.AreEqual("ZAux", package.PackageVersion.Title);
            Assert.AreEqual("My Company", package.PackageVersion.DistributorName);
            Assert.AreEqual(expectedPackageVersion, package.PackageVersion.Version);
            Assert.IsTrue(package.Package.Branches.Contains("main"));
            Assert.IsTrue(package.Package.Branches.Contains("release/1.0"));
        }

        [TestMethod]
        [DataRow("2.0.0.0", "1.0.0.0")]
        [DataRow("1.0.0.0", "1.0.0.0")]
        public async Task FetchPackageAsync_EffectiveVersion(string effectiveVersion, string expectedPackageVersion)
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux",
                        Title = "ZAux",
                        Version = "1.0.0.0",
                        DistributorName = "My Company",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1"
                    }
                },
                Connected = true
            };

            var automationInterfaceMock = new Mock<IAutomationInterface>();
            automationInterfaceMock.Setup(x => x.ResolveEffectiveVersionAsync("TestProject1", "Untitled1", "ZAux")).Returns(Task.Run(() => effectiveVersion));

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers, automationInterfaceMock.Object);

            var package = new PackageItem { ProjectName = "TestProject1", PlcName = "Untitled1", Config = new ConfigPlcPackage { Name = "ZAux" } };

            await twinpack.FetchPackageAsync(package);

            Assert.AreEqual("ZAux", package.Package.Name);
            Assert.AreEqual("ZAux", package.PackageVersion.Name);
            Assert.AreEqual("ZAux", package.PackageVersion.Title);
            Assert.AreEqual("My Company", package.PackageVersion.DistributorName);
            Assert.AreEqual(expectedPackageVersion, package.PackageVersion.Version);
            Assert.IsTrue(package.Package.Branches.Contains("main"));
            Assert.IsTrue(package.Package.Branches.Contains("release/1.0"));
        }

        [TestMethod]
        public async Task DownloadPackageVersionAsync_PackagesWithDependencies()
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        { 
                            new PackageVersionGetResponse() { Name = "ZCore" },
                            new PackageVersionGetResponse() { Name = "ZPlatform" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib1" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib1",
                        Version = "1.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib2" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib2",
                        Version = "2.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib3" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib3",
                        Version = "2.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1"
                    }
                },
                Connected = true
            };

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers);

            var downloadedPackageVersions = new List<PackageItem>();
            var package = new PackageItem { Config = new ConfigPlcPackage { Name = "ZAux" } };

            downloadedPackageVersions = await twinpack.DownloadPackagesAsync(new List<PackageItem> { package }, 
                new TwinpackService.DownloadPackageOptions
                {
                    IncludeProvidedPackages = false,
                    IncludeDependencies = true,
                    ForceDownload = true
                });

            Assert.AreEqual(6, downloadedPackageVersions.Count());
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZCore"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZPlatform"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZAux"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib1"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib2"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib3"));

            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ZCore").PackageServer);
            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ZPlatform").PackageServer);
            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ZAux").PackageServer);
            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ExternalLib1").PackageServer);
            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ExternalLib2").PackageServer);
            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ExternalLib3").PackageServer);

            Assert.IsTrue(downloadedPackageVersions.Select(x => x.Package.Branches).FirstOrDefault()?.Count() == 2);
        }

        [TestMethod]
        public async Task DownloadPackageVersionAsync_IgnoreProvidedPackages()
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore" },
                            new PackageVersionGetResponse() { Name = "ZPlatform" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib1" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib1",
                        Version = "1.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib2" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib2",
                        Version = "2.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib3" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib3",
                        Version = "2.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1"
                    }
                },
                Connected = true
            };

            var config = new Config
            {
                Projects = new List<ConfigProject>
                {
                    new ConfigProject
                    {
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject { Name = "ZCore" },
                            new ConfigPlcProject { Name = "ZPlatform" },
                            new ConfigPlcProject { Name = "ZAux" }
                        }
                    }
                }
            };

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers, null, config);

            var downloadedPackageVersions = new List<PackageItem>();
            var package = new PackageItem { Config = new ConfigPlcPackage { Name = "ZAux" } };

            downloadedPackageVersions = await twinpack.DownloadPackagesAsync(new List<PackageItem> { package },
                new TwinpackService.DownloadPackageOptions
                {
                    IncludeProvidedPackages = false,
                    IncludeDependencies = true,
                    ForceDownload = true
                });


            Assert.AreEqual(3, downloadedPackageVersions.Count());
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib1"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib2"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib3"));

            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ExternalLib1").PackageServer);
            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ExternalLib2").PackageServer);
            Assert.AreEqual(packageServer, downloadedPackageVersions.FirstOrDefault(x => x.PackageVersion.Name == "ExternalLib3").PackageServer);
        }

        [TestMethod]
        public async Task DownloadPackageVersionAsync_DownloadProvidedPackages()
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore" },
                            new PackageVersionGetResponse() { Name = "ZPlatform" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore",
                        Version = "1.5.0.1",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib1" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib1",
                        Version = "1.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib2" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib2",
                        Version = "2.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib3" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib3",
                        Version = "2.0.0.0",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1"
                    }
                },
                Connected = true
            };

            var config = new Config
            {
                Projects = new List<ConfigProject>
                {
                    new ConfigProject
                    {
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject { Name = "ZCore" },
                            new ConfigPlcProject { Name = "ZPlatform" },
                            new ConfigPlcProject { Name = "ZAux" }
                        }
                    }
                }
            };

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers, null, config);

            var downloadedPackageVersions = new List<PackageItem>();
            var package = new PackageItem { Config = new ConfigPlcPackage { Name = "ZAux" } };

            downloadedPackageVersions = await twinpack.DownloadPackagesAsync(new List<PackageItem> { package },
                new TwinpackService.DownloadPackageOptions
                {
                    IncludeProvidedPackages = true,
                    IncludeDependencies = true,
                    ForceDownload = true
                });

            Assert.AreEqual(6, downloadedPackageVersions.Count());
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZCore"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZPlatform"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZAux"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib1"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib2"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib3"));
        }

        private TwinpackService BuildMultiProjectConfig(out Config config)
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore", Version = "1.5.0.1" },
                            new PackageVersionGetResponse() { Name = "ZPlatform", Version = "1.5.0.1" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZAux", Version = "1.5.0.2", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore", Version = "1.5.0.2" },
                            new PackageVersionGetResponse() { Name = "ZPlatform", Version = "1.5.0.2" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore", Version = "1.5.0.1" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform", Version = "1.5.0.2", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore", Version = "1.5.0.2" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform", Version = "1.2.0.1", Branch = "release/1.2", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore", Version = "1.5.0.2" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform", Version = "1.4.0.1", Branch = "release/1.4", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore", Version = "1.5.0.2" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib1", Version = "1.2.3.4" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore", Version = "1.5.0.2", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib1", Version = "2.2.3.4" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore", Version = "1.5.0.3", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ExternalLib1", Version = "2.2.3.4" },
                        }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib1", Version = "1.2.3.4", Branch = "main", Configuration = "Release", Target = "TC3.1",
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ExternalLib1", Version = "2.2.3.4", Branch = "main", Configuration = "Release", Target = "TC3.1",
                    }
                },
                Connected = true
            };

            config = new Config
            {
                Projects = new List<ConfigProject>
                {
                    new ConfigProject
                    {
                        Name = "ZCore Project",
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject { Name = "ZCore" },
                        }
                    },
                    new ConfigProject
                    {
                        Name = "ZPlatform Project",
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject { Name = "ZPlatform",
                                Packages = new List<ConfigPlcPackage> {
                                    new ConfigPlcPackage { Name = "ZCore", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                                    new ConfigPlcPackage { Name = "ExternalLib1", Version = "1.2.3.4", Branch = "main", Configuration = "Release", Target = "TC3.1" }
                                }
                            },
                        }
                    },
                    new ConfigProject
                    {
                        Name = "ZAux Project",
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject { Name = "ZAux", Version = "1.5.0.1",
                                Packages = new List<ConfigPlcPackage> {
                                    new ConfigPlcPackage { Name = "ZCore", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                                    new ConfigPlcPackage { Name = "ZPlatform", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                                }
                            },
                        }
                    }
                }
            };

            var packageServers = new PackageServerCollection { packageServer };
            return new TwinpackService(packageServers, null, config);
        }

        [TestMethod]
        public async Task RestoreAsync_NoIncludedProvidedPackages_NoIncludedDependencies()
        {
            var twinpack = BuildMultiProjectConfig(out _);

            var packages = await twinpack.RestorePackagesAsync(
                new TwinpackService.RestorePackageOptions
                {
                    IncludeProvidedPackages = false,
                    IncludeDependencies = false,
                    ForceDownload = false
                });

            Assert.AreEqual(1, packages.Count());
            Assert.AreEqual("ExternalLib1", packages.FirstOrDefault().Config.Name);
            Assert.AreEqual("ExternalLib1", packages.FirstOrDefault().PackageVersion.Name);
            Assert.AreEqual("1.2.3.4", packages.FirstOrDefault().Config.Version);
            Assert.AreEqual("1.2.3.4", packages.FirstOrDefault().PackageVersion.Version);
            Assert.AreEqual("ZPlatform Project", packages.FirstOrDefault().ProjectName);
            Assert.AreEqual("ZPlatform", packages.FirstOrDefault().PlcName);
        }

        [TestMethod]
        public async Task RestoreAsync_IncludedProvidedPackages_NoIncludedDependencies()
        {
            var twinpack = BuildMultiProjectConfig(out _);

            var packages = await twinpack.RestorePackagesAsync(
                new TwinpackService.RestorePackageOptions
                {
                    IncludeProvidedPackages = true,
                    IncludeDependencies = false,
                    ForceDownload = false
                });

            Assert.AreEqual(4, packages.Count());

            var platformCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", platformCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", platformCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.1", platformCorePackage.PackageVersion.Version);

            var platformExternalPackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ExternalLib1");
            Assert.AreEqual("ExternalLib1", platformExternalPackage.PackageVersion.Name);
            Assert.AreEqual("1.2.3.4", platformExternalPackage.Config.Version);
            Assert.AreEqual("1.2.3.4", platformExternalPackage.PackageVersion.Version);

            var auxCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", auxCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", auxCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.1", auxCorePackage.PackageVersion.Version);

            var auxPlatformPackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZPlatform");
            Assert.AreEqual("ZPlatform", auxPlatformPackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", auxPlatformPackage.Config.Version);
            Assert.AreEqual("1.5.0.1", auxPlatformPackage.PackageVersion.Version);
        }

        [TestMethod]
        public async Task RestoreAsync_IncludedProvidedPackages_IncludedDependencies()
        {
            var twinpack = BuildMultiProjectConfig(out _);

            var packages = await twinpack.RestorePackagesAsync(
                new TwinpackService.RestorePackageOptions
                {
                    IncludeProvidedPackages = true,
                    IncludeDependencies = true,
                    ForceDownload = false
                });

            Assert.AreEqual(5, packages.Count());

            var platformCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", platformCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", platformCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.1", platformCorePackage.PackageVersion.Version);

            var platformExternalPackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ExternalLib1");
            Assert.AreEqual("ExternalLib1", platformExternalPackage.PackageVersion.Name);
            Assert.AreEqual("1.2.3.4", platformExternalPackage.Config.Version);
            Assert.AreEqual("1.2.3.4", platformExternalPackage.PackageVersion.Version);

            var auxCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", auxCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", auxCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.1", auxCorePackage.PackageVersion.Version);

            var auxPlatformPackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZPlatform");
            Assert.AreEqual("ZPlatform", auxPlatformPackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", auxPlatformPackage.Config.Version);
            Assert.AreEqual("1.5.0.1", auxPlatformPackage.PackageVersion.Version);

            var auxExternalPackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ExternalLib1");
            Assert.AreEqual("ExternalLib1", auxExternalPackage.PackageVersion.Name);
            Assert.AreEqual("1.2.3.4", auxExternalPackage.Config.Version);
            Assert.AreEqual("1.2.3.4", auxExternalPackage.PackageVersion.Version);
        }

        [DataTestMethod]
        [DataRow("1.5.0.2", "1.5.0.2")]
        [DataRow("1.5.0.3", "1.5.0.3")]
        [DataRow(null, "1.5.0.3")]
        [DataRow("1.5.0.100", "1.5.0.100")]
        public async Task UpdateAsync_NoIncludedProvidedPackages_NoIncludedDependencies_WithFilters1(string version, string expectedVersion)
        {
            Config config;
            var twinpack = BuildMultiProjectConfig(out config);

            var packages = await twinpack.UpdatePackagesAsync(
                new TwinpackService.UpdatePackageFilters
                { 
                    ProjectName = "ZPlatform Project",
                    PlcName = "ZPlatform",
                    Packages = new[] { "ZCore" },
                    Versions = new[] { version }
                },
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = false,
                    IncludeDependencies = false,
                    ForceDownload = false
                });


            Assert.AreEqual(0, packages.Count());
            Assert.IsFalse(config.Projects.First(x => x.Name == "ZCore Project").Plcs.First(x => x.Name == "ZCore").Packages.Any() );
            Assert.AreEqual(expectedVersion, config.Projects.First(x => x.Name == "ZPlatform Project").Plcs.First(x => x.Name == "ZPlatform").Packages.First(x => x.Name == "ZCore").Version );
            Assert.AreEqual("1.2.3.4", config.Projects.First(x => x.Name == "ZPlatform Project").Plcs.First(x => x.Name == "ZPlatform").Packages.First(x => x.Name == "ExternalLib1").Version );
            Assert.AreEqual("1.5.0.1", config.Projects.First(x => x.Name == "ZAux Project").Plcs.First(x => x.Name == "ZAux").Packages.First(x => x.Name == "ZCore").Version );
            Assert.AreEqual("1.5.0.1", config.Projects.First(x => x.Name == "ZAux Project").Plcs.First(x => x.Name == "ZAux").Packages.First(x => x.Name == "ZPlatform").Version );
        }

        [DataTestMethod]
        [DataRow("release/1.2", null, "1.2.0.1")]
        [DataRow("release/1.4", null, "1.4.0.1")]
        [DataRow("release/1.4", "", "1.4.0.1")]
        [DataRow(null, "1.5.0.2", "1.5.0.2")]
        public async Task UpdateAsync_NoIncludedProvidedPackages_NoIncludedDependencies_WithBranch(string branch, string version, string expectedVersion)
        {
            Config config;
            var twinpack = BuildMultiProjectConfig(out config);
            config.Projects[2].Plcs[0].Packages.First(x => x.Name == "ZPlatform").Branch = "release/1.4";

            var packages = await twinpack.UpdatePackagesAsync(
                new TwinpackService.UpdatePackageFilters
                {
                    ProjectName = "ZAux Project",
                    PlcName = "ZAux",
                    Packages = new[] { "ZPlatform" },
                    Versions = version == null ? null : new[] { version },
                    Branches = new[] { branch }
                },
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = false,
                    IncludeDependencies = false,
                    ForceDownload = false
                });


            Assert.AreEqual(0, packages.Count());
            Assert.IsFalse(config.Projects.First(x => x.Name == "ZCore Project").Plcs.First(x => x.Name == "ZCore").Packages.Any());
            Assert.AreEqual("1.5.0.1", config.Projects.First(x => x.Name == "ZPlatform Project").Plcs.First(x => x.Name == "ZPlatform").Packages.First(x => x.Name == "ZCore").Version);
            Assert.AreEqual("1.2.3.4", config.Projects.First(x => x.Name == "ZPlatform Project").Plcs.First(x => x.Name == "ZPlatform").Packages.First(x => x.Name == "ExternalLib1").Version);
            Assert.AreEqual("1.5.0.1", config.Projects.First(x => x.Name == "ZAux Project").Plcs.First(x => x.Name == "ZAux").Packages.First(x => x.Name == "ZCore").Version);
            Assert.AreEqual(expectedVersion, config.Projects.First(x => x.Name == "ZAux Project").Plcs.First(x => x.Name == "ZAux").Packages.First(x => x.Name == "ZPlatform").Version);
        }

        [DataTestMethod]
        [DataRow("1.5.0.2", "1.5.0.2")]
        [DataRow("1.5.0.3", "1.5.0.3")]
        [DataRow(null, "1.5.0.2")]
        public async Task UpdateAsync_NoIncludedProvidedPackages_NoIncludedDependencies_WithFilters2(string version, string expectedVersion)
        {
            Config config;
            var twinpack = BuildMultiProjectConfig(out config);

            var packages = await twinpack.UpdatePackagesAsync(
                new TwinpackService.UpdatePackageFilters
                {
                    ProjectName = "ZAux Project",
                    PlcName = "ZAux",
                    Packages = new[] { "ZPlatform" },
                    Versions = new[] { version }
                },
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = false,
                    IncludeDependencies = false,
                    ForceDownload = false
                });


            Assert.AreEqual(0, packages.Count());
            Assert.IsFalse(config.Projects.First(x => x.Name == "ZCore Project").Plcs.First(x => x.Name == "ZCore").Packages.Any());
            Assert.AreEqual("1.5.0.1", config.Projects.First(x => x.Name == "ZPlatform Project").Plcs.First(x => x.Name == "ZPlatform").Packages.First(x => x.Name == "ZCore").Version);
            Assert.AreEqual("1.2.3.4", config.Projects.First(x => x.Name == "ZPlatform Project").Plcs.First(x => x.Name == "ZPlatform").Packages.First(x => x.Name == "ExternalLib1").Version);
            Assert.AreEqual("1.5.0.1", config.Projects.First(x => x.Name == "ZAux Project").Plcs.First(x => x.Name == "ZAux").Packages.First(x => x.Name == "ZCore").Version);
            Assert.AreEqual(expectedVersion, config.Projects.First(x => x.Name == "ZAux Project").Plcs.First(x => x.Name == "ZAux").Packages.First(x => x.Name == "ZPlatform").Version);
        }

        [TestMethod]
        public async Task UpdateAsync_NoIncludedProvidedPackages_NoIncludedDependencies()
        {
            var twinpack = BuildMultiProjectConfig(out _);

            var packages = await twinpack.UpdatePackagesAsync(
                new TwinpackService.UpdatePackageFilters { },
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = false,
                    IncludeDependencies = false,
                    ForceDownload = false
                });

            Assert.AreEqual(1, packages.Count());
            Assert.AreEqual("ExternalLib1", packages.FirstOrDefault().Config.Name);
            Assert.AreEqual("ExternalLib1", packages.FirstOrDefault().PackageVersion.Name);
            Assert.AreEqual("2.2.3.4", packages.FirstOrDefault().Config.Version);
            Assert.AreEqual("2.2.3.4", packages.FirstOrDefault().PackageVersion.Version);
            Assert.AreEqual("ZPlatform Project", packages.FirstOrDefault().ProjectName);
            Assert.AreEqual("ZPlatform", packages.FirstOrDefault().PlcName);
        }


        [TestMethod]
        public async Task UpdateAsync_IncludedProvidedPackages_NoIncludedDependencies()
        {
            var twinpack = BuildMultiProjectConfig(out _);

            var packages = await twinpack.UpdatePackagesAsync(
                new TwinpackService.UpdatePackageFilters { },
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = true,
                    IncludeDependencies = false,
                    ForceDownload = false
                });

            Assert.AreEqual(4, packages.Count());

            var platformCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", platformCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.3", platformCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.3", platformCorePackage.PackageVersion.Version);

            var platformExternalPackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ExternalLib1");
            Assert.AreEqual("ExternalLib1", platformExternalPackage.PackageVersion.Name);
            Assert.AreEqual("2.2.3.4", platformExternalPackage.Config.Version);
            Assert.AreEqual("2.2.3.4", platformExternalPackage.PackageVersion.Version);

            var auxCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", auxCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.3", auxCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.3", auxCorePackage.PackageVersion.Version);

            var auxPlatformPackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZPlatform");
            Assert.AreEqual("ZPlatform", auxPlatformPackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.2", auxPlatformPackage.Config.Version);
            Assert.AreEqual("1.5.0.2", auxPlatformPackage.PackageVersion.Version);
        }

        [TestMethod]
        public async Task UpdateAsync_IncludedProvidedPackages_IncludedDependencies()
        {
            var twinpack = BuildMultiProjectConfig(out _);

            var packages = await twinpack.UpdatePackagesAsync(
                new TwinpackService.UpdatePackageFilters { },
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = true,
                    IncludeDependencies = true,
                    ForceDownload = false
                });

            Assert.AreEqual(5, packages.Count());

            var platformCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", platformCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.3", platformCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.3", platformCorePackage.PackageVersion.Version);

            var platformExternalPackage = packages.FirstOrDefault(x => x.PlcName == "ZPlatform" && x.Config.Name == "ExternalLib1");
            Assert.AreEqual("ExternalLib1", platformExternalPackage.PackageVersion.Name);
            Assert.AreEqual("2.2.3.4", platformExternalPackage.Config.Version);
            Assert.AreEqual("2.2.3.4", platformExternalPackage.PackageVersion.Version);

            var auxCorePackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZCore");
            Assert.AreEqual("ZCore", auxCorePackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.3", auxCorePackage.Config.Version);
            Assert.AreEqual("1.5.0.3", auxCorePackage.PackageVersion.Version);

            var auxPlatformPackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ZPlatform");
            Assert.AreEqual("ZPlatform", auxPlatformPackage.PackageVersion.Name);
            Assert.AreEqual("1.5.0.2", auxPlatformPackage.Config.Version);
            Assert.AreEqual("1.5.0.2", auxPlatformPackage.PackageVersion.Version);

            var auxExternalPackage = packages.FirstOrDefault(x => x.PlcName == "ZAux" && x.Config.Name == "ExternalLib1");
            Assert.AreEqual("ExternalLib1", auxExternalPackage.PackageVersion.Name);
            Assert.AreEqual("2.2.3.4", auxExternalPackage.Config.Version);
            Assert.AreEqual("2.2.3.4", auxExternalPackage.PackageVersion.Version);
        }

        private TwinpackService BuildMultiBranchDependencies()
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>()
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZCore", Version = "1.5.0.1", Branch = "fix/some-fix", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>()
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "ZPlatform", Version = "1.5.0.1", Branch = "main", Configuration = "Release", Target = "TC3.1",
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse() { Name = "ZCore", Version = "1.5.0.1", Branch = "main" }
                        }
                    },
                    
                },
                Connected = true
            };


            var packageServers = new PackageServerCollection { packageServer };
            return new TwinpackService(packageServers);
        }

        [TestMethod]
        public async Task AffectedPackages_PreferPassedPackageItems()
        {
            var twinpack = BuildMultiBranchDependencies();

            var packages = await twinpack.AffectedPackagesAsync(
                new List<PackageItem>()
                {
                    new PackageItem() { Config = new ConfigPlcPackage { Name = "ZPlatform", Version = "1.5.0.1", Branch = "main" } },
                    new PackageItem() { Config = new ConfigPlcPackage { Name = "ZCore", Version = "1.5.0.1", Branch = "fix/some-fix" } }
                });

            Assert.AreEqual(2, packages.Count());

            var corePackage = packages.FirstOrDefault(x => x.PackageVersion.Name == "ZCore");
            Assert.AreEqual("ZCore", corePackage.PackageVersion.Name);
            Assert.AreEqual("fix/some-fix", corePackage.PackageVersion.Branch);

            var platformPackage = packages.FirstOrDefault(x => x.PackageVersion.Name == "ZPlatform");
            Assert.AreEqual("ZPlatform", platformPackage.PackageVersion.Name);
            Assert.AreEqual("main", platformPackage.PackageVersion.Branch);
        }
    }
}
