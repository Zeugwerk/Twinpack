using Microsoft.VisualStudio.TestTools.UnitTesting;﻿
﻿using System.Collections.Generic;﻿
﻿using System.Linq;﻿
﻿using System.Threading.Tasks;﻿
﻿using Twinpack.Core;﻿
﻿using Twinpack.Models;﻿
﻿using Twinpack.Protocol.Api;﻿
﻿
﻿
﻿namespace TwinpackTests﻿
﻿{﻿
﻿    using Microsoft.VisualStudio.TestTools.UnitTesting;﻿
﻿    using System.Collections.Generic;﻿
﻿    using System.Linq;﻿
﻿    using System.Threading.Tasks;﻿
﻿    using Twinpack.Configuration;﻿
﻿    using Twinpack.Core;﻿
﻿    using Twinpack.Models;﻿
﻿    using Twinpack.Protocol.Api;﻿
﻿
﻿
﻿    [TestClass]﻿
﻿    public class SystemTestHeaded : SystemTest﻿
﻿    {﻿
﻿        [ClassInitialize]﻿
﻿        public static void SetUp(TestContext context)﻿
﻿        {﻿
﻿            SetUpRepository();﻿
﻿            _config = ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution").GetAwaiter().GetResult();﻿
﻿            _visualStudio = new VisualStudio();﻿
﻿            _automationInterface = _visualStudio.Open(_config);﻿
﻿        }﻿
﻿    }﻿
﻿
﻿    [TestClass]﻿
﻿    public class SystemTestHeadless : SystemTest﻿
﻿    {﻿
﻿
﻿        [ClassInitialize]﻿
﻿        public static void SetUp(TestContext context)﻿
﻿        {﻿
﻿            SetUpRepository();﻿
﻿            _config = ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution").GetAwaiter().GetResult();﻿
﻿            _visualStudio = null;﻿
﻿            _automationInterface = new AutomationInterfaceHeadless(_config);﻿
﻿        }﻿
﻿    }﻿
﻿
﻿
﻿    public class SystemTest﻿
﻿    {﻿
﻿        protected static Config _config;﻿
﻿        protected static VisualStudio _visualStudio;﻿
﻿        protected static IAutomationInterface _automationInterface;﻿
﻿        protected static PackageServerMock _packageServer;﻿
﻿
﻿        protected static void SetUpRepository()﻿
﻿        {﻿
﻿            _packageServer = new PackageServerMock﻿
﻿            {﻿
﻿                PackageVersionItems = new List<PackageVersionGetResponse>﻿
﻿                {﻿
﻿                    new PackageVersionGetResponse()﻿
﻿                    {﻿
﻿                        Name = "Plc1",﻿
﻿                        Title = "Plc1",﻿
﻿                        Version = "1.2.3.3",﻿
﻿                        Branch = "main",﻿
﻿                        Framework = "My Framework",﻿
﻿                        Configuration = "Release",﻿
﻿                        Target = "TC3.1",﻿
﻿                        DistributorName = "My Company",﻿
﻿                        Dependencies = new List<PackageVersionGetResponse> { new PackageVersionGetResponse() { Name = "Tc3_Module" } }﻿
﻿                    },﻿
﻿                    new PackageVersionGetResponse()﻿
﻿                    {﻿
﻿                        Name = "PlcLibrary1",﻿
﻿                        Title = "PlcLibrary1",﻿
﻿                        Version = "1.2.3.3",﻿
﻿                        Branch = "main",﻿
﻿                        Framework = "My Framework",﻿
﻿                        Configuration = "Release",﻿
﻿                        Target = "TC3.1",﻿
﻿                        DistributorName = "My Company",﻿
﻿                        Dependencies = new List<PackageVersionGetResponse> { new PackageVersionGetResponse() { Name = "Tc3_Module" } }﻿
﻿                    },﻿
﻿                    new PackageVersionGetResponse()﻿
﻿                    {﻿
﻿                        Name = "PlcLibrary1",﻿
﻿                        Title = "PlcLibrary1",﻿
﻿                        Version = "1.2.3.4",﻿
﻿                        Branch = "main",﻿
﻿                        Framework = "My Framework",﻿
﻿                        Configuration = "Release",﻿
﻿                        Target = "TC3.1",﻿
﻿                        DistributorName = "My Company",﻿
﻿                        Dependencies = new List<PackageVersionGetResponse> { new PackageVersionGetResponse() { Name = "Tc3_Module" } }﻿
﻿                    },﻿
﻿                    new PackageVersionGetResponse()﻿
﻿                    {﻿
﻿                        Name = "Tc3_Module",﻿
﻿                        Title = "Tc3_Module",﻿
﻿                        Version = null,﻿
﻿                        Branch = "main",﻿
﻿                        Configuration = "Release",﻿
﻿                        Target = "TC3.1",﻿
﻿                        DistributorName = "Beckhoff Automation GmbH"﻿
﻿                    }﻿
﻿                },﻿
﻿                Connected = true﻿
﻿            };﻿
﻿        }﻿
﻿
﻿        [TestMethod]﻿
﻿        public async Task AddPackage_WithDependency_Async()﻿
﻿        {﻿
﻿            var packageServers = new PackageServerCollection { _packageServer };﻿
﻿            var twinpack = new TwinpackService(packageServers, automationInterface: _automationInterface, config: _config);﻿
﻿
﻿            // cleanup﻿
﻿            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1" } } });﻿
﻿            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module" } } });﻿
﻿
﻿            // act - uninstall previously installed package﻿
﻿            var package = new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = "1.2.3.4" } };﻿
﻿            await twinpack.UninstallPackagesAsync(new List<PackageItem> { package });﻿
﻿            var uninstalled = await twinpack.UninstallPackagesAsync(new List<PackageItem> { package });﻿
﻿
﻿            Assert.IsFalse(uninstalled, "Package that is not installed does not throw if trying to uninstall");﻿
﻿
﻿
﻿            // act - add package, including dependencies﻿
﻿            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } }, ﻿
﻿                new TwinpackService.AddPackageOptions { IncludeDependencies = true, ForceDownload = false });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            var configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(configPackages.Count, 2);﻿
﻿            Assert.AreEqual(true, configPackages.Any(x => x.Name == "PlcLibrary1"));﻿
﻿            Assert.AreEqual(true, configPackages.Any(x => x.Name == "Tc3_Module"));﻿
﻿
﻿            // check if plcproj was updated as well﻿
﻿            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            var plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(plcprojPackages.Count, 2);﻿
﻿            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "PlcLibrary1"));﻿
﻿            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "Tc3_Module"));﻿
﻿
﻿
﻿            // act remove PlcLibrary1 package﻿
﻿            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(configPackages.Count, 1);﻿
﻿            Assert.AreEqual(true, configPackages.Any(x => x.Name == "Tc3_Module"));﻿
﻿
﻿            // check if plcproj was updated as well﻿
﻿            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(plcprojPackages.Count, 1);﻿
﻿            Assert.AreEqual(true, plcprojPackages.Any(x => x.Name == "Tc3_Module"));﻿
﻿
﻿            // act remove Tc3_Module package﻿
﻿            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module", Version = null } } });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(configPackages.Count, 0);﻿
﻿
﻿            // check if plcproj was updated as well﻿
﻿            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(plcprojPackages.Count, 0);﻿
﻿        }﻿
﻿
﻿        [DataTestMethod]﻿
﻿        [DataRow(true, false, true, true, true)]﻿
﻿        [DataRow(true, true, true, true, true)]﻿
﻿        [DataRow(false, false, true, false, true)]﻿
﻿        public async Task AddPackage_WithOptions_Async(bool hide, bool library, bool optional, bool publishAll, bool qualifiedOnly)﻿
﻿        {﻿
﻿            var packageServers = new PackageServerCollection { _packageServer };﻿
﻿            var twinpack = new TwinpackService(packageServers, automationInterface: _automationInterface, config: _config);﻿
﻿
﻿            // cleanup﻿
﻿            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1" } } });﻿
﻿            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module" } } });﻿
﻿
﻿            // act - add package, including dependencies﻿
﻿            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", ﻿
﻿                Config = new ConfigPlcPackage﻿
﻿                {﻿
﻿                    Name = "PlcLibrary1", ﻿
﻿                    Version = null, ﻿
﻿                    Options = new AddPlcLibraryOptions﻿
﻿                    { ﻿
﻿                        HideWhenReferencedAsDependency = hide,﻿
﻿                        LibraryReference = library,﻿
﻿                        Optional = optional,﻿
﻿                        PublishSymbolsInContainer = publishAll,﻿
﻿                        QualifiedOnly = qualifiedOnly﻿
﻿                    }﻿
﻿                } ﻿
﻿            } }, new TwinpackService.AddPackageOptions { IncludeDependencies = true, ForceDownload = false });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            var configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            var package = configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(configPackages.Count, 2);﻿
﻿            Assert.IsNotNull(package?.Name);﻿
﻿            Assert.AreEqual(hide, package.Options?.HideWhenReferencedAsDependency);﻿
﻿            Assert.AreEqual(library, package.Options?.LibraryReference);﻿
﻿            Assert.AreEqual(optional, package.Options?.Optional);﻿
﻿            Assert.AreEqual(publishAll, package.Options?.PublishSymbolsInContainer);﻿
﻿            Assert.AreEqual(qualifiedOnly, package.Options?.QualifiedOnly);﻿
﻿
﻿            // check if plcproj was updated as well﻿
﻿            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            var plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            var plcprojPackage = plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(2, plcprojPackages.Count);﻿
﻿            Assert.IsNotNull(plcprojPackage?.Name);﻿
﻿            Assert.AreEqual(hide, plcprojPackage.Options?.HideWhenReferencedAsDependency);﻿
﻿            Assert.AreEqual(library, plcprojPackage.Options?.LibraryReference);﻿
﻿            Assert.AreEqual(optional, plcprojPackage.Options?.Optional);﻿
﻿            Assert.AreEqual(publishAll, plcprojPackage.Options?.PublishSymbolsInContainer);﻿
﻿            Assert.AreEqual(qualifiedOnly, plcprojPackage.Options?.QualifiedOnly);﻿
﻿
﻿            // act remove PlcLibrary1 package﻿
﻿            await twinpack.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1", Version = null } } });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(configPackages.Count, 1);﻿
﻿
﻿            // check if plcproj was updated as well﻿
﻿            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            plcprojPackages = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            Assert.AreEqual(plcprojPackages.Count, 1);﻿
﻿        }﻿
﻿
﻿        [Ignore]﻿
﻿        [TestMethod]﻿
﻿        public async Task AddPackage_WithPackageThatIsNotInstalled_Async()﻿
﻿        {﻿
﻿            _visualStudio?.Close();﻿
﻿
﻿            // first prepare the plc, add a library, which does not exist﻿
﻿            var packageServers = new PackageServerCollection { _packageServer };﻿
﻿            var twinpackHeadless = new TwinpackService(packageServers, automationInterface: new AutomationInterfaceHeadless(_config), config: _config);﻿
﻿
﻿            // cleanup﻿
﻿            await twinpackHeadless.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1" } } });﻿
﻿            await twinpackHeadless.RemovePackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module" } } });﻿
﻿
﻿            await twinpackHeadless.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1",﻿
﻿                Config = new ConfigPlcPackage﻿
﻿                {﻿
﻿                    Name = "PlcLibrary1", Version = "6.6.6.6",﻿
﻿                }﻿
﻿            } }, new TwinpackService.AddPackageOptions { IncludeDependencies = true, ForceDownload = false });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            var configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            var package = configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(configPackages.Count, 2);﻿
﻿            Assert.AreEqual("6.6.6.6", package.Version);﻿
﻿
﻿            // check if plcproj references were not updated﻿
﻿            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            var plcprojPlc = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            var plcprojPackages = plcprojPlc.Packages;﻿
﻿            var plcprojPackage = plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(plcprojPackages.Count, 2);﻿
﻿            Assert.AreEqual("6.6.6.6", plcprojPackage.Version);﻿
﻿
﻿            _visualStudio?.Open(_config);﻿
﻿            // now add package with the real automation interface and make sure it works﻿
﻿            var twinpack = new TwinpackService(packageServers, automationInterface: _automationInterface, config: _config);﻿
﻿
﻿            await twinpackHeadless.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1",﻿
﻿                Config = new ConfigPlcPackage﻿
﻿                {﻿
﻿                    Name = "PlcLibrary1", Version = "1.2.3.4",﻿
﻿                }﻿
﻿            } }, new TwinpackService.AddPackageOptions { IncludeDependencies = true, ForceDownload = false });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            configPackages = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1").Packages;﻿
﻿            package = configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(configPackages.Count, 2);﻿
﻿            Assert.AreEqual("1.2.3.4", package.Version);﻿
﻿
﻿            // check if plcproj references were not updated﻿
﻿            plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            plcprojPlc = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            plcprojPackages = plcprojPlc.Packages;﻿
﻿            plcprojPackage = plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(plcprojPackages.Count, 2);﻿
﻿            Assert.AreEqual("1.2.3.4", plcprojPackage.Version);﻿
﻿        }﻿
﻿
﻿        [DataTestMethod]﻿
﻿        [DataRow("3.3.3.3", "3.3.3.5", "3.3.3.7")]﻿
﻿        [DataRow("2.3.3.3", "3.3.3.5", "3.3.3.7")]﻿
﻿        public async Task SetPackageVersion_VersionNotExistingOnPackageServers_Async(string newVersion1, string newVersion2, string newVersion3)﻿
﻿        {﻿
﻿            var packageServers = new PackageServerCollection { _packageServer };﻿
﻿            var twinpack = new TwinpackService(packageServers, automationInterface: _automationInterface, config: _config);﻿
﻿
﻿            // cleanup﻿
﻿            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1" } } });﻿
﻿            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module" } } });﻿
﻿
﻿            // act - add package, including dependencies﻿
﻿            await twinpack.SetPackageVersionAsync(newVersion1, new TwinpackService.SetPackageVersionOptions { ProjectName = "TestProject", PlcName = "Plc1", SyncFrameworkPackages = true });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            var plc = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            var configPackages = plc.Packages;﻿
﻿            Assert.AreEqual(newVersion1, plc?.Version);﻿
﻿            Assert.AreEqual(newVersion1, configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Version);﻿
﻿            Assert.AreEqual(null, configPackages.FirstOrDefault(x => x.Name == "Tc3_Module")?.Version);﻿
﻿
﻿            // check if plcproj references were not updated﻿
﻿            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            var plcprojPlc = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            var plcprojPackages = plcprojPlc.Packages;﻿
﻿            var plcprojPackage = plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(newVersion1, plcprojPlc?.Version);﻿
﻿            Assert.AreEqual("1.2.3.4", plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Version);﻿
﻿            Assert.AreEqual(null, plcprojPackages.FirstOrDefault(x => x.Name == "Tc3_Module")?.Version);﻿
﻿
﻿            // act - add package, including dependencies﻿
﻿            await twinpack.SetPackageVersionAsync(newVersion2, new TwinpackService.SetPackageVersionOptions { SyncFrameworkPackages = true,﻿
﻿                PreferredFrameworkBranch = "My Special Branch",﻿
﻿                PreferredFrameworkTarget = "My Special Target",﻿
﻿                PreferredFrameworkConfiguration = "My Special Configuration",﻿
﻿            });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            plc = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            configPackages = plc.Packages;﻿
﻿            Assert.AreEqual(newVersion2, plc?.Version);﻿
﻿            Assert.AreEqual(newVersion2, configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Version);﻿
﻿            Assert.AreEqual("My Special Branch", configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Branch);﻿
﻿            Assert.AreEqual("My Special Target", configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Target);﻿
﻿            Assert.AreEqual("My Special Configuration", configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Configuration);﻿
﻿            Assert.AreEqual(null, configPackages.FirstOrDefault(x => x.Name == "Tc3_Module")?.Version);﻿
﻿
﻿
﻿            // act - add package, including dependencies﻿
﻿            await twinpack.SetPackageVersionAsync(newVersion3, new TwinpackService.SetPackageVersionOptions { SyncFrameworkPackages = false });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            plc = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            configPackages = plc.Packages;﻿
﻿            Assert.AreEqual(newVersion3, plc?.Version);﻿
﻿            Assert.AreEqual(newVersion2, configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Version);﻿
﻿            Assert.AreEqual(null, configPackages.FirstOrDefault(x => x.Name == "Tc3_Module")?.Version);﻿
﻿        }﻿
﻿
﻿        [DataTestMethod]﻿
﻿        [DataRow("1.2.3.3")]﻿
﻿        [DataRow("1.2.3.4")]﻿
﻿        public async Task SetPackageVersion_VersionExistingOnPackageServers_Async(string newVersion)﻿
﻿        {﻿
﻿            _config = ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution").GetAwaiter().GetResult();﻿
﻿            var packageServers = new PackageServerCollection { _packageServer };﻿
﻿            var twinpack = new TwinpackService(packageServers, automationInterface: _automationInterface, config: _config);﻿
﻿
﻿            // cleanup﻿
﻿            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "PlcLibrary1" } } });﻿
﻿            await twinpack.AddPackagesAsync(new List<PackageItem> { new PackageItem { ProjectName = "TestProject", PlcName = "Plc1", Config = new ConfigPlcPackage { Name = "Tc3_Module" } } });﻿
﻿
﻿            // act - add package, including dependencies﻿
﻿            await twinpack.SetPackageVersionAsync(newVersion, new TwinpackService.SetPackageVersionOptions { ProjectName = "TestProject", PlcName = "Plc1", SyncFrameworkPackages = true, ﻿
﻿                PreferredFrameworkBranch = "My Special Branch",﻿
﻿                PreferredFrameworkTarget = "My Special Target",﻿
﻿                PreferredFrameworkConfiguration = "My Special Configuration",﻿
﻿            });﻿
﻿
﻿            // check if config was updated correctly﻿
﻿            var plc = _config.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            var configPackages = plc.Packages;﻿
﻿            Assert.AreEqual(newVersion, plc?.Version);﻿
﻿            Assert.AreEqual(newVersion, configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Version);﻿
﻿            Assert.AreEqual("main", configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Branch);﻿
﻿            Assert.AreEqual("TC3.1", configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Target);﻿
﻿            Assert.AreEqual("Release", configPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Configuration);﻿
﻿            Assert.AreEqual(null, configPackages.FirstOrDefault(x => x.Name == "Tc3_Module")?.Version);﻿
﻿
﻿            // check if plcproj was updated as well﻿
﻿            var plcproj = await ConfigFactory.CreateFromSolutionFileAsync(_config.WorkingDirectory, continueWithoutSolution: false, packageServers: packageServers);﻿
﻿            var plcprojPlc = plcproj.Projects.FirstOrDefault(x => x.Name == "TestProject").Plcs.FirstOrDefault(x => x.Name == "Plc1");﻿
﻿            var plcprojPackages = plcprojPlc.Packages;﻿
﻿            var plcprojPackage = plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1");﻿
﻿            Assert.AreEqual(newVersion, plcprojPlc?.Version);﻿
﻿            Assert.AreEqual(newVersion, plcprojPackages.FirstOrDefault(x => x.Name == "PlcLibrary1")?.Version);﻿
﻿            Assert.AreEqual(null, plcprojPackages.FirstOrDefault(x => x.Name == "Tc3_Module")?.Version);﻿
﻿        }﻿
﻿    }﻿
﻿}﻿
