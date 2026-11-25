using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    /// <summary>
    /// Which automation interface set-version uses for each PLC. No interface means "do not touch plcproj
    /// files" (zkmake set-version --skip-plc-update), for the top level and for modules alike. With an
    /// interface, modules get a headless interface of their own and are edited like a headless top level.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class SetPackageVersionInterfaceTest
    {
        const string TopLevel = "top-level";
        const string Modules = "modules";

        string _directory;
        string _previousDirectory;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "twinpack-set-version-interface-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _previousDirectory = Environment.CurrentDirectory;
            InstalledLibraries.Use(new List<XElement>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            InstalledLibraries.Use(null);
            Environment.CurrentDirectory = _previousDirectory;
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [DataTestMethod]
        [DataRow(TopLevel, "1.6.0.29")]
        [DataRow(TopLevel, "1.6.0.30")]
        [DataRow(Modules,  "1.6.0.29")]
        [DataRow(Modules,  "1.6.0.30")]
        public async Task WithoutAutomationInterface_SyncFrameworkPackages_UpdatesConfigOnly(string scope, string version)
        {
            var config = Workspace(scope, out var plcFile);
            var configFile = Path.Combine(Path.GetDirectoryName(plcFile), "..", ".Zeugwerk", "config.json");
            var plcBefore = File.ReadAllBytes(plcFile);
            var twinpack = new TwinpackService(new PackageServerCollection { FrameworkServer() }, automationInterface: null, config: config);

            await twinpack.SetPackageVersionAsync(version, SyncOptions());

            CollectionAssert.AreEqual(plcBefore, File.ReadAllBytes(plcFile), "the plcproj must not be touched without an automation interface");
            var saved = SavedPlc(configFile);
            Assert.AreEqual(version, saved.Version);
            Assert.AreEqual(version, saved.Packages.Single(x => x.Name == "ZCore").Version);
        }

        [DataTestMethod]
        [DataRow(TopLevel)]
        [DataRow(Modules)]
        public async Task WithHeadlessInterface_SyncFrameworkPackages_UpdatesPlcproj(string scope)
        {
            var config = Workspace(scope, out var plcFile);
            var twinpack = new TwinpackService(new PackageServerCollection { FrameworkServer() }, new AutomationInterfaceHeadless(config), config);

            await twinpack.SetPackageVersionAsync("1.6.0.29", SyncOptions());

            var plcproj = File.ReadAllText(plcFile);
            StringAssert.Contains(plcproj, "<ProjectVersion>1.6.0.29</ProjectVersion>");
            StringAssert.Contains(plcproj, "ZCore, 1.6.0.29 (Zeugwerk GmbH)");
        }

        /// <summary>
        /// 1.6.0.30 is not published. Only an interface that edits files directly may reference a version that
        /// does not exist yet. Under a headed top level a module is still edited by its own headless interface,
        /// so it gets the reference, like a headless top level does.
        /// </summary>
        [DataTestMethod]
        [DataRow(TopLevel, false)]
        [DataRow(Modules,  true)]
        public async Task WithHeadedInterface_UnpublishedFrameworkVersion_ReferencedWhereHeadlessEdits(string scope, bool referenced)
        {
            var config = Workspace(scope, out var plcFile);
            var twinpack = new TwinpackService(new PackageServerCollection { FrameworkServer() }, HeadedMock().Object, config);

            await twinpack.SetPackageVersionAsync("1.6.0.30", SyncOptions());

            Assert.AreEqual(referenced, File.ReadAllText(plcFile).Contains("ZCore, 1.6.0.30 (Zeugwerk GmbH)"));
        }

        static TwinpackService.SetPackageVersionOptions SyncOptions()
        {
            return new TwinpackService.SetPackageVersionOptions
            {
                SyncFrameworkPackages = true,
                PreferredFrameworkBranch = "release/1.6",
                PreferredFrameworkConfiguration = "Snapshot",
                PreferredFrameworkTarget = "TC3.1"
            };
        }

        static ConfigPlcProject SavedPlc(string configFile)
        {
            var saved = System.Text.Json.JsonSerializer.Deserialize<Config>(File.ReadAllText(configFile));
            return saved.Projects.Single(x => x.Name == "ZPlatform").Plcs.Single(x => x.Name == "ZPlatform");
        }

        /// <summary>
        /// Top level: ZPlatform is a PLC of the loaded configuration. Modules: the loaded configuration only
        /// lists the module ModA, whose own configuration (loaded from the working directory) holds ZPlatform.
        /// </summary>
        Config Workspace(string scope, out string plcFile)
        {
            if (scope == TopLevel)
            {
                var topLevel = ZPlatformConfig(_directory);
                ConfigFactory.Save(topLevel);
                plcFile = topLevel.Projects[0].Plcs[0].FilePath;
                return topLevel;
            }

            var module = ZPlatformConfig(Path.Combine(_directory, "ModA"));
            ConfigFactory.Save(module);
            plcFile = module.Projects[0].Plcs[0].FilePath;
            Environment.CurrentDirectory = _directory;
            return new Config
            {
                FilePath = Path.Combine(_directory, ".Zeugwerk", "config.json"),
                WorkingDirectory = _directory,
                Projects = new List<ConfigProject>(),
                Modules = new List<string> { "ModA" }
            };
        }

        static Config ZPlatformConfig(string directory)
        {
            var plcproj = Path.Combine(directory, "ZPlatform", "ZPlatform.plcproj");
            Directory.CreateDirectory(Path.GetDirectoryName(plcproj));
            File.WriteAllText(plcproj,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Project DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                "<PropertyGroup><Name>ZPlatform</Name><Title>ZPlatform</Title><ProjectVersion>1.6.0.0</ProjectVersion><Company>Zeugwerk GmbH</Company></PropertyGroup>" +
                "</Project>");

            return new Config
            {
                FilePath = Path.Combine(directory, ".Zeugwerk", "config.json"),
                WorkingDirectory = directory,
                Projects = new List<ConfigProject>
                {
                    new ConfigProject
                    {
                        Name = "ZPlatform",
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject
                            {
                                Name = "ZPlatform",
                                ProjectName = "ZPlatform",
                                DistributorName = "Zeugwerk GmbH",
                                Version = "1.6.0.0",
                                FilePath = plcproj,
                                Packages = new List<ConfigPlcPackage>
                                {
                                    new ConfigPlcPackage { Name = "ZCore", Version = "1.6.0.1", Branch = "release/1.6", Configuration = "Distribution", Target = "TC3.1", DistributorName = "Zeugwerk GmbH" }
                                }
                            }
                        }
                    }
                }
            };
        }

        static PackageVersionGetResponse FrameworkPackage(string name, string version)
        {
            return new PackageVersionGetResponse
            {
                Name = name, Title = name, Version = version, Branch = "release/1.6", Configuration = "Distribution", Target = "TC3.1",
                DistributorName = "Zeugwerk GmbH", Framework = "Zeugwerk"
            };
        }

        static PackageServerMock FrameworkServer()
        {
            return new PackageServerMock
            {
                Connected = true,
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    FrameworkPackage("ZCore", "1.6.0.1"),
                    FrameworkPackage("ZCore", "1.6.0.29"),
                    FrameworkPackage("ZPlatform", "1.6.0.29")
                }
            };
        }

        static Mock<IAutomationInterface> HeadedMock()
        {
            var automation = new Mock<IAutomationInterface>();
            automation.Setup(x => x.CloseAllPackageRelatedWindowsAsync(It.IsAny<List<PackageItem>>())).Returns(Task.CompletedTask);
            automation.Setup(x => x.AddPackageAsync(It.IsAny<PackageItem>())).Returns(Task.CompletedTask);
            automation.Setup(x => x.SetPackageVersionAsync(It.IsAny<ConfigPlcProject>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            automation.Setup(x => x.SaveAllAsync()).Returns(Task.CompletedTask);
            automation.Setup(x => x.IsPackageInstalledAsync(It.IsAny<PackageItem>())).ReturnsAsync(false);
            automation.Setup(x => x.InstallPackageAsync(It.IsAny<PackageItem>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            return automation;
        }

        sealed class InstalledLibraries : AutomationInterfaceHeadless
        {
            InstalledLibraries() : base(new Config()) { }

            public static void Use(List<XElement> libraries) => LocalRepository = libraries;
        }
    }
}