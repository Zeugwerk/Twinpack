using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Models.Api;


namespace TwinpackTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Twinpack.Core;
    using Twinpack.Models;
    using Twinpack.Models.Api;


    [TestClass]
    public class SystemTestHeaded : SystemTest
    {
        [ClassInitialize]
        public static void SetUp(TestContext context)
        {
            SetUpRepository();
            _config = ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution").GetAwaiter().GetResult();
            _automationInterface = new VisualStudio().Open(_config);
        }
    }

    [TestClass]
    public class SystemTestHeadless : SystemTest
    {

        [ClassInitialize]
        public static void SetUp(TestContext context)
        {
            SetUpRepository();
            _config = ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution").GetAwaiter().GetResult();
            _automationInterface = new AutomationInterfaceHeadless(_config);
        }
    }


    public class SystemTest
    {
        protected static Config _config;
        protected static IAutomationInterface _automationInterface;
        protected static PackageServerMock _packageServer;


        protected static void SetUpRepository()
        {
            _packageServer = new PackageServerMock
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
        }

        [TestMethod]
        public async Task AddPackage_WithDependency_Async()
        {
            var packageServers = new PackageServerCollection { _packageServer };
            var twinpack = new TwinpackService(packageServers, automationInterface: _automationInterface, config: _config);

            // cleanup
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1" } } });
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module" } } });

            // act - uninstall previously installed package
            var package = new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = "1.2.3.4" } };
            await twinpack.UninstallPackagesAsync(new List<PackageItem> { package });
            var uninstalled = await twinpack.UninstallPackagesAsync(new List<PackageItem> { package });

            Assert.IsFalse(uninstalled, "Package that is not installed does not throw if trying to uninstall");


            // act - add package, including dependencies
            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } }, 
                new TwinpackService.AddPackageOptions { AddDependencies = true, ForceDownload = false });

            // check if config was updated correctly
            var configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 2);
            Assert.AreEqual(true, configPackages.Any(x => x.Name == "PlcLibrary1"));
            Assert.AreEqual(true, configPackages.Any(x => x.Name == "Tc3_Module"));

            // check if plcproj was updated as well
            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
            var plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 2);
            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "PlcLibrary1"));
            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "Tc3_Module"));


            // act remove PlcLibrary1 package
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } });

            // check if config was updated correctly
            configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 1);
            Assert.AreEqual(true, configPackages.Any(x => x.Name == "Tc3_Module"));

            // check if plcproj was updated as well
            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 1);
            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "Tc3_Module"));

            // act remove Tc3_Module package
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module", Version = null } } });

            // check if config was updated correctly
            configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 0);

            // check if plcproj was updated as well
            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 0);
        }

        [DataTestMethod]
        [DataRow(true, false, true, true, true)]
        [DataRow(true, true, true, true, true)]
        [DataRow(false, false, true, false, true)]
        public async Task AddPackage_WithOptions_Async(bool hide, bool library, bool optional, bool publishAll, bool qualifiedOnly)
        {
            var packageServers = new PackageServerCollection { _packageServer };
            var twinpack = new TwinpackService(packageServers, automationInterface: _automationInterface, config: _config);

            // cleanup
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1" } } });
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module" } } });

            // act - add package, including dependencies
            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", 
                Config = new ConfigPlcPackage
                {
                    Name = "PlcLibrary1", 
                    Version = null, 
                    Options = new AddPlcLibraryOptions
                    { 
                        HideWhenReferencedAsDependency = hide,
                        LibraryReference = library,
                        Optional = optional,
                        PublishSymbolsInContainer = publishAll,
                        QualifiedOnly = qualifiedOnly
                    }
                } 
            } }, new TwinpackService.AddPackageOptions { AddDependencies = true, ForceDownload = false });

            // check if config was updated correctly
            var configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            var package = configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");
            Assert.AreEqual(configPackages.Count, 2);
            Assert.IsNotNull(package?.Name);
            Assert.AreEqual(hide, package.Options?.HideWhenReferencedAsDependency);
            Assert.AreEqual(library, package.Options?.LibraryReference);
            Assert.AreEqual(optional, package.Options?.Optional);
            Assert.AreEqual(publishAll, package.Options?.PublishSymbolsInContainer);
            Assert.AreEqual(qualifiedOnly, package.Options?.QualifiedOnly);

            // check if plcproj was updated as well
            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
            var plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            var plcprojPackage = plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");
            Assert.AreEqual(2, plcprojPackages.Count);
            Assert.IsNotNull(plcprojPackage?.Name);
            Assert.AreEqual(hide, plcprojPackage.Options?.HideWhenReferencedAsDependency);
            Assert.AreEqual(library, plcprojPackage.Options?.LibraryReference);
            Assert.AreEqual(optional, plcprojPackage.Options?.Optional);
            Assert.AreEqual(publishAll, plcprojPackage.Options?.PublishSymbolsInContainer);
            Assert.AreEqual(qualifiedOnly, plcprojPackage.Options?.QualifiedOnly);

            // act remove PlcLibrary1 package
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } });

            // check if config was updated correctly
            configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 1);

            // check if plcproj was updated as well
            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 1);
        }
    }
}
