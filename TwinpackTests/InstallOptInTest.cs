using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Commands;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    /// <summary>
    /// Install intent is explicit. A headless interface installs only when a caller passes an
    /// installer (opt-in), modules install exactly when their top level does, set-version honours
    /// SkipInstall, and restore passes --skip-install on.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class InstallOptInTest
    {
        const string NoInstaller = "headless";
        const string WithInstaller = "headless+installer";
        const string Mocked = "mock";
        const string TopLevel = "top-level";
        const string Modules = "modules";

        string _directory;
        string _previousDirectory;
        RecordingInstaller _installer;
        List<string> _mockInstalls;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "twinpack-install-opt-in-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _previousDirectory = Environment.CurrentDirectory;
            _installer = new RecordingInstaller();
            _mockInstalls = new List<string>();
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

        [TestMethod]
        public void Headless_CanInstallOnlyWithInstaller()
        {
            Assert.IsFalse(new AutomationInterfaceHeadless(new Config()).CanInstallPackages);
            Assert.IsFalse(new AutomationInterfaceHeadless(new Config(), installer: null).CanInstallPackages);
            Assert.IsTrue(new AutomationInterfaceHeadless(new Config(), _installer).CanInstallPackages);
        }

        [TestMethod]
        public void Module_GetsTheTopLevelInstaller()
        {
            Assert.IsFalse(new AutomationInterfaceHeadless(new Config()).ForModule(new Config()).CanInstallPackages);
            Assert.IsTrue(new AutomationInterfaceHeadless(new Config(), _installer).ForModule(new Config()).CanInstallPackages);
        }

        [TestMethod]
        public async Task Headless_InstallPackageAsync_UsesInstallerWhenThereIsOne()
        {
            var package = new PackageItem { PackageVersion = new PackageVersionGetResponse { Name = "App", Version = "1.0.0.0" } };

            await new AutomationInterfaceHeadless(new Config()).InstallPackageAsync(package, "cache");
            Assert.AreEqual("", Installed());

            await new AutomationInterfaceHeadless(new Config(), _installer).InstallPackageAsync(package, "cache");
            Assert.AreEqual("App", Installed());
            Assert.AreEqual("cache", _installer.CachePaths.Single());
        }

        // App depends on Dep, and only App is in the cache folder.
        [DataTestMethod]
        [DataRow(false, false, "App,Dep")]
        [DataRow(false, true,  "App")]
        [DataRow(true,  false, "")]
        [DataRow(true,  true,  "")]
        public async Task AddPackagesAsync_HeadlessWithInstaller(bool skipInstall, bool skipDownload, string expected)
        {
            var config = AppConfig();
            var cache = Path.Combine(_directory, "libraries");
            Directory.CreateDirectory(Path.Combine(cache, "TC3.1"));
            File.WriteAllText(Path.Combine(cache, "TC3.1", "App_1.0.0.0.library"), "app");
            var twinpack = new TwinpackService(new PackageServerCollection { AppServer() }, new AutomationInterfaceHeadless(config, _installer), config);

            await twinpack.AddPackagesAsync(
                new List<PackageItem>
                {
                    new PackageItem
                    {
                        ProjectName = "P",
                        PlcName = "PLC",
                        Config = new ConfigPlcPackage { Name = "App", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1", DistributorName = "Vendor" }
                    }
                },
                new TwinpackService.AddPackageOptions { SkipInstall = skipInstall, SkipDownload = skipDownload, DownloadPath = cache });

            Assert.AreEqual(expected, Installed());
        }

        // ZCore 1.6.0.29 is published, Struckig is a non-framework library that PurgePackages re-adds.
        [DataTestMethod]
        //      interface      scope     purge  skipInstall expected installs
        [DataRow(Mocked,        TopLevel, true,  false, "Struckig,ZCore")]
        [DataRow(Mocked,        TopLevel, true,  true,  "")]
        [DataRow(Mocked,        TopLevel, false, false, "ZCore")]
        [DataRow(Mocked,        TopLevel, false, true,  "")]
        [DataRow(WithInstaller, TopLevel, false, false, "ZCore")]
        [DataRow(WithInstaller, TopLevel, false, true,  "")]
        [DataRow(NoInstaller,   TopLevel, false, false, "")]
        // modules: the top level's installer decides, a headed (mock) top level gives modules none
        [DataRow(WithInstaller, Modules,  false, false, "ZCore")]
        [DataRow(WithInstaller, Modules,  true,  false, "Struckig,ZCore")]
        [DataRow(WithInstaller, Modules,  false, true,  "")]
        [DataRow(NoInstaller,   Modules,  false, false, "")]
        [DataRow(Mocked,        Modules,  false, false, "")]
        public async Task SetPackageVersionAsync_SkipInstall(string ai, string scope, bool purge, bool skipInstall, string expected)
        {
            var config = Workspace(scope);
            IAutomationInterface automation;
            switch (ai)
            {
                case Mocked: automation = Mock(); break;
                case WithInstaller: automation = new AutomationInterfaceHeadless(config, _installer); break;
                default: automation = new AutomationInterfaceHeadless(config); break;
            }
            var twinpack = new TwinpackService(new PackageServerCollection { FrameworkServer() }, automation, config);

            await twinpack.SetPackageVersionAsync("1.6.0.29", new TwinpackService.SetPackageVersionOptions
            {
                SyncFrameworkPackages = true,
                PurgePackages = purge,
                SkipInstall = skipInstall,
                PreferredFrameworkBranch = "release/1.6",
                PreferredFrameworkConfiguration = "Snapshot",
                PreferredFrameworkTarget = "TC3.1"
            });

            Assert.AreEqual(expected, Installed());
        }

        [DataTestMethod]
        [DataRow(false, false, false, false)]
        [DataRow(true,  false, false, false)]
        [DataRow(false, true,  false, false)]
        [DataRow(false, false, true,  false)]
        [DataRow(false, false, false, true)]
        public void RestoreCommand_PassesOptionsOn(bool skipInstall, bool skipDownload, bool includeProvided, bool forceDownload)
        {
            var options = RestoreCommand.CreateOptions(new RestoreCommand.Settings
            {
                SkipInstall = skipInstall,
                SkipDownload = skipDownload,
                IncludeProvidedPackages = includeProvided,
                ForceDownload = forceDownload
            });

            Assert.AreEqual(skipInstall, options.SkipInstall);
            Assert.AreEqual(skipDownload, options.SkipDownload);
            Assert.AreEqual(includeProvided, options.IncludeProvidedPackages);
            Assert.AreEqual(forceDownload, options.ForceDownload);
        }

        string Installed()
        {
            return string.Join(",", _installer.Installed.Concat(_mockInstalls).OrderBy(x => x, StringComparer.Ordinal));
        }

        IAutomationInterface Mock()
        {
            var automation = new Mock<IAutomationInterface>();
            automation.Setup(x => x.CloseAllPackageRelatedWindowsAsync(It.IsAny<List<PackageItem>>())).Returns(Task.CompletedTask);
            automation.Setup(x => x.AddPackageAsync(It.IsAny<PackageItem>())).Returns(Task.CompletedTask);
            automation.Setup(x => x.RemoveAllPackagesAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
            automation.Setup(x => x.SetPackageVersionAsync(It.IsAny<ConfigPlcProject>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            automation.Setup(x => x.SaveAllAsync()).Returns(Task.CompletedTask);
            automation.Setup(x => x.IsPackageInstalledAsync(It.IsAny<PackageItem>())).ReturnsAsync(false);
            automation.Setup(x => x.InstallPackageAsync(It.IsAny<PackageItem>(), It.IsAny<string>()))
                .Callback<PackageItem, string>((package, cachePath) => _mockInstalls.Add(package.PackageVersion.Name))
                .Returns(Task.CompletedTask);
            return automation.Object;
        }

        Config AppConfig()
        {
            var plcproj = Path.Combine(_directory, "P", "PLC.plcproj");
            Directory.CreateDirectory(Path.GetDirectoryName(plcproj));
            File.WriteAllText(plcproj,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Project DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                "<PropertyGroup><Name>PLC</Name></PropertyGroup>" +
                "</Project>");

            return new Config
            {
                FilePath = Path.Combine(_directory, ".Zeugwerk", "config.json"),
                WorkingDirectory = _directory,
                Projects = new List<ConfigProject>
                {
                    new ConfigProject
                    {
                        Name = "P",
                        Plcs = new List<ConfigPlcProject> { new ConfigPlcProject { Name = "PLC", ProjectName = "P", FilePath = plcproj, Packages = new List<ConfigPlcPackage>() } }
                    }
                }
            };
        }

        static PackageServerMock AppServer()
        {
            return new PackageServerMock
            {
                Connected = true,
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse
                    {
                        Name = "App", Title = "App", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1", DistributorName = "Vendor",
                        Dependencies = new List<PackageVersionGetResponse> { new PackageVersionGetResponse { Name = "Dep", Version = "1.0.0.0", DistributorName = "Vendor" } }
                    },
                    new PackageVersionGetResponse
                    {
                        Name = "Dep", Title = "Dep", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1", DistributorName = "Vendor",
                        Dependencies = new List<PackageVersionGetResponse>()
                    }
                }
            };
        }

        // Top level: ZPlatform is a PLC of the loaded configuration. Modules: ZPlatform lives in module ModA.
        Config Workspace(string scope)
        {
            if (scope == TopLevel)
                return ZPlatformConfig(_directory);

            ConfigFactory.Save(ZPlatformConfig(Path.Combine(_directory, "ModA")));
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
                                    new ConfigPlcPackage { Name = "ZCore", Version = "1.6.0.1", Branch = "release/1.6", Configuration = "Distribution", Target = "TC3.1", DistributorName = "Zeugwerk GmbH" },
                                    new ConfigPlcPackage { Name = "Struckig", Version = "0.11.0.0", Branch = "release/0.11", Configuration = "Snapshot", Target = "TC3.1", DistributorName = "Stefan Besler" }
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
                    FrameworkPackage("ZPlatform", "1.6.0.29"),
                    new PackageVersionGetResponse { Name = "Struckig", Title = "Struckig", Version = "0.11.0.0", Branch = "release/0.11", Configuration = "Snapshot", Target = "TC3.1", DistributorName = "Stefan Besler" }
                }
            };
        }

        sealed class RecordingInstaller : IPackageInstaller
        {
            public List<string> Installed { get; } = new List<string>();
            public List<string> CachePaths { get; } = new List<string>();

            public Task InstallPackageAsync(PackageItem package, string cachePath)
            {
                Installed.Add(package.PackageVersion.Name);
                CachePaths.Add(cachePath);
                return Task.CompletedTask;
            }
        }

        sealed class InstalledLibraries : AutomationInterfaceHeadless
        {
            InstalledLibraries() : base(new Config()) { }

            public static void Use(List<XElement> libraries) => LocalRepository = libraries;
        }
    }
}