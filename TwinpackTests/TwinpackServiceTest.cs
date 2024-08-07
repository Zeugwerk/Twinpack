using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Models.Api;


namespace TwinpackTests
{
    [TestClass]
    public class TwinpackServiceTest
    {
        PackageServerCollection _packageServers;
        PackageServerMock _packageServer1;
        PackageServerMock _packageServer2;
        PackageServerMock _packageServerNotConnected;
        TwinpackService _twinpack;

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

        [TestMethod]
        public async Task RetrieveInstalledPackagesAsync_WithoutSearchTerm()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };

            var packages = (await _twinpack.RetrieveUsedPackagesAsync(config, searchTerm: null)).ToList();

            Assert.AreEqual(3, packages.Count);
            Assert.AreEqual(2, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(1, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveInstalledPackagesAsync_WithSearchTerm_PackageName()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };

            var packages = (await _twinpack.RetrieveUsedPackagesAsync(config, searchTerm: "4")).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(1, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(0, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveInstalledPackagesAsync_WithSearchTerm_DistributorName()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };

            var packages = (await _twinpack.RetrieveUsedPackagesAsync(config, searchTerm: "My Distributor 5")).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(1, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(0, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveInstalledPackagesAsync_WithSearchTerm_DisplayName()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = "1.1.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
                new ConfigPlcPackage() { Name = "Package 6", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1" },
            };

            var packages = (await _twinpack.RetrieveUsedPackagesAsync(config, searchTerm: "My Displayname")).ToList();

            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual(1, packages.Where(x => x.Used != null).Count());
            Assert.AreEqual(0, packages.Where(x => x.Used == null).Count());
        }

        [TestMethod]
        public async Task RetrieveInstalledPackagesAsync_ResolvingNeeded()
        {
            var config = new Config { Projects = new List<ConfigProject> { new ConfigProject { Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { } } } } };
            config.Projects[0].Plcs[0].Packages = new List<ConfigPlcPackage>
            {
                new ConfigPlcPackage() { Name = "Package 4", Version = null, Branch = null, Target = null, Configuration = null },
                new ConfigPlcPackage() { Name = "Package 5", Version = "1.0.0.0", Branch = null, Target = null, Configuration = null },
                new ConfigPlcPackage() { Name = "Package 6", Version = "0.0.0.0", Branch = null, Target = null, Configuration = null },
            };

            var packages = (await _twinpack.RetrieveUsedPackagesAsync(config, searchTerm: null)).ToList();

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

        [TestMethod]
        public async Task ResolvePackageAsync_PackageMetadataIsPopulated()
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

            var downloadedPackageVersions = new List<PackageItem>();
            var package = new PackageItem { Config = new ConfigPlcPackage { Name = "ZAux" } };

            await twinpack.ResolvePackageAsync(package);

            Assert.AreEqual("ZAux", package.Package.Name);
            Assert.AreEqual("ZAux", package.PackageVersion.Name);
            Assert.AreEqual("1.5.0.1", package.PackageVersion.Version);
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

            downloadedPackageVersions = await twinpack.DownloadPackagesAsync(new List<PackageItem> { package }, includeDependencies: true, forceDownload: true);

            Assert.AreEqual(6, downloadedPackageVersions.Count());
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZCore"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZPlatform"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ZAux"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib1"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib2"));
            Assert.IsTrue(downloadedPackageVersions.Any(x => x.PackageVersion.Name == "ExternalLib3"));

            Assert.IsTrue(downloadedPackageVersions.Select(x => x.Package.Branches).FirstOrDefault()?.Count() == 2);
        }

        [TestMethod]
        public async Task SystemTest_UninstallAddRemove_Async_Headed()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");

            await SystemTest_UninstallAddRemove_Async(new VisualStudio().Open(config), config);
        }

        [TestMethod]
        public async Task SystemTest_UninstallAddRemove_Async_Headless()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");

            await SystemTest_UninstallAddRemove_Async(new AutomationInterfaceHeadless(config), config);
        }

        public async Task SystemTest_UninstallAddRemove_Async(IAutomationInterface automationInterface, Config config)
        {
            var packageServer = new PackageServerMock
            {
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse()
                    {
                        Name = "PlcLibrary1",
                        Title = "PlcLibrary1",
                        Version = "1.2.3.3",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        DistributorName = "My Company",
                        Dependencies = new List<PackageVersionGetResponse> { new PackageVersionGetResponse() { Name = "Tc3_Module" } }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "PlcLibrary1",
                        Title = "PlcLibrary1",
                        Version = "1.2.3.4",
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        DistributorName = "My Company",
                        Dependencies = new List<PackageVersionGetResponse> { new PackageVersionGetResponse() { Name = "Tc3_Module" } }
                    },
                    new PackageVersionGetResponse()
                    {
                        Name = "Tc3_Module",
                        Title = "Tc3_Module",
                        Version = null,
                        Branch = "main",
                        Configuration = "Release",
                        Target = "TC3.1",
                        DistributorName = "Beckhoff Automation GmbH"
                    }
                },
                Connected = true
            };

            var packageServers = new PackageServerCollection { packageServer };
            var twinpack = new TwinpackService(packageServers, automationInterface: automationInterface, config: config);


            // act - uninstall previously installed package
            var package = new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = "1.2.3.4" } };
            await twinpack.UninstallPackagesAsync(new List<PackageItem> { package });
            var uninstalled = await twinpack.UninstallPackagesAsync(new List<PackageItem> { package });

            Assert.IsFalse(uninstalled, "Package that is not installed does not throw if trying to uninstall");


            // act - add package, including dependencies
            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } }, 
                new TwinpackService.AddPackageOptions { AddDependencies = true, ForceDownload = false });

            // check if config was updated correctly
            var configPackages = config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 2);
            Assert.AreEqual(true, configPackages.Any(x => x.Name == "PlcLibrary1"));
            Assert.AreEqual(true, configPackages.Any(x => x.Name == "Tc3_Module"));

            // check if plcproj was updated as well
            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(config.WorkingDirectory);
            var plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 2);
            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "PlcLibrary1"));
            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "Tc3_Module"));


            // act remove PlcLibrary1 package
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } });

            // check if config was updated correctly
            configPackages = config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 1);
            Assert.AreEqual(true, configPackages.Any(x => x.Name == "Tc3_Module"));

            // check if plcproj was updated as well
            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(config.WorkingDirectory);
            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 1);
            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "Tc3_Module"));


            // act remove Tc3_Module package
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module", Version = null } } });

            // check if config was updated correctly
            configPackages = config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 0);

            // check if plcproj was updated as well
            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(config.WorkingDirectory);
            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 0);
        }
    }
}
