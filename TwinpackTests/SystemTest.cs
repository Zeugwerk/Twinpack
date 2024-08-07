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
    public class SystemTest
    {
        PackageServerCollection _packageServers;
        PackageServerMock _packageServer1;
        PackageServerMock _packageServer2;
        PackageServerMock _packageServerNotConnected;
        TwinpackService _twinpack;

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
            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
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
            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 1);
            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "Tc3_Module"));


            // act remove Tc3_Module package
            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module", Version = null } } });

            // check if config was updated correctly
            configPackages = config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(configPackages.Count, 0);

            // check if plcproj was updated as well
            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);
            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;
            Assert.AreEqual(plcprojPackages.Count, 0);
        }
    }
}
