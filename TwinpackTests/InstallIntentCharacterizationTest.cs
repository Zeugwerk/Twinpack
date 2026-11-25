using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// WP0.4 characterization tests. They record which libraries <see cref="TwinpackService"/> hands to
    /// <see cref="IAutomationInterface.InstallPackageAsync"/> today, for each package operation, automation
    /// interface and install option. They describe the current behaviour, not the desired one: a row that
    /// changes must be a deliberate decision, made in the commit that changes it.
    ///
    /// Two things are observed for every row:
    /// <list type="bullet">
    /// <item>the calls the given automation interface receives (Moq callback or a recording headless subclass);</item>
    /// <item>the <c>[install] Name Version</c> line TwinpackService logs right before every call. It also covers
    /// the interfaces TwinpackService creates itself for modules, which a test cannot hand in.</item>
    /// </list>
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class InstallIntentCharacterizationTest
    {
        // automation interface passed to TwinpackService
        const string NoAi = "null";
        const string Headless = "headless";
        const string Mocked = "mock";

        // where the PLCs live
        const string TopLevel = "top-level";
        const string Modules = "modules";

        const string Vendor = "Vendor";
        static readonly Regex InstallLine = new Regex(@"^INFO \[install\] (\S+) (\S+)$");

        string _directory;
        string _previousDirectory;
        LoggingConfiguration _previousLogConfiguration;
        MemoryTarget _log;
        List<string> _installCalls;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "twinpack-install-intent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _previousDirectory = Environment.CurrentDirectory;
            _installCalls = new List<string>();

            _log = new MemoryTarget { Layout = "${level:uppercase=true} ${message}" };
            _previousLogConfiguration = NLog.LogManager.Configuration;
            var logConfig = new LoggingConfiguration();
            logConfig.AddRuleForAllLevels(_log);
            NLog.LogManager.Configuration = logConfig;

            // Headless reads the machine's library index into a static field. An empty index keeps
            // the rows independent of the libraries installed on the machine running the tests.
            RecordingHeadless.UseLocalRepository(new List<XElement>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            RecordingHeadless.UseLocalRepository(null);
            NLog.LogManager.Configuration = _previousLogConfiguration;
            Environment.CurrentDirectory = _previousDirectory;
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        #region AddPackagesAsync, RestorePackagesAsync, UpdatePackagesAsync

        // App depends on Dep. The cache folder holds App only, so a run with SkipDownload shows which
        // libraries come from the cache and which are left out.

        [DataTestMethod]
        //      interface skipInstall skipDownload expected InstallPackageAsync calls
        [DataRow(NoAi,     false, false, "")]
        [DataRow(NoAi,     false, true,  "")]
        [DataRow(NoAi,     true,  false, "")]
        [DataRow(NoAi,     true,  true,  "")]
        [DataRow(Headless, false, false, "")]
        [DataRow(Headless, false, true,  "")]
        [DataRow(Headless, true,  false, "")]
        [DataRow(Headless, true,  true,  "")]
        [DataRow(Mocked,   false, false, "App,Dep")]
        [DataRow(Mocked,   false, true,  "App")]
        [DataRow(Mocked,   true,  false, "")]
        [DataRow(Mocked,   true,  true,  "")]
        public async Task AddPackagesAsync_TopLevel(string ai, bool skipInstall, bool skipDownload, string expected)
        {
            var config = AppDepConfig(referenced: false);
            var twinpack = new TwinpackService(new PackageServerCollection { AppDepServer() }, CreateAutomationInterface(ai, config), config);

            await twinpack.AddPackagesAsync(new List<PackageItem> { AppRequest() }, new TwinpackService.AddPackageOptions
            {
                SkipInstall = skipInstall,
                SkipDownload = skipDownload,
                DownloadPath = CacheWithApp()
            });

            AssertInstalls(expected);
            AssertReferenced(config, "App", "Dep");
        }

        [DataTestMethod]
        [DataRow(NoAi,     false, false, "")]
        [DataRow(NoAi,     false, true,  "")]
        [DataRow(NoAi,     true,  false, "")]
        [DataRow(NoAi,     true,  true,  "")]
        [DataRow(Headless, false, false, "")]
        [DataRow(Headless, false, true,  "")]
        [DataRow(Headless, true,  false, "")]
        [DataRow(Headless, true,  true,  "")]
        [DataRow(Mocked,   false, false, "App,Dep")]
        [DataRow(Mocked,   false, true,  "App")]
        [DataRow(Mocked,   true,  false, "")]
        [DataRow(Mocked,   true,  true,  "")]
        public async Task RestorePackagesAsync_TopLevel(string ai, bool skipInstall, bool skipDownload, string expected)
        {
            var config = AppDepConfig(referenced: true);
            var twinpack = new TwinpackService(new PackageServerCollection { AppDepServer() }, CreateAutomationInterface(ai, config), config);

            await twinpack.RestorePackagesAsync(new TwinpackService.RestorePackageOptions
            {
                SkipInstall = skipInstall,
                SkipDownload = skipDownload,
                DownloadPath = CacheWithApp()
            });

            AssertInstalls(expected);
            AssertReferenced(config, "App", "Dep");
        }

        [DataTestMethod]
        [DataRow(NoAi,     false, false, "")]
        [DataRow(NoAi,     false, true,  "")]
        [DataRow(NoAi,     true,  false, "")]
        [DataRow(NoAi,     true,  true,  "")]
        [DataRow(Headless, false, false, "")]
        [DataRow(Headless, false, true,  "")]
        [DataRow(Headless, true,  false, "")]
        [DataRow(Headless, true,  true,  "")]
        [DataRow(Mocked,   false, false, "App,Dep")]
        [DataRow(Mocked,   false, true,  "App")]
        [DataRow(Mocked,   true,  false, "")]
        [DataRow(Mocked,   true,  true,  "")]
        public async Task UpdatePackagesAsync_TopLevel(string ai, bool skipInstall, bool skipDownload, string expected)
        {
            var config = AppDepConfig(referenced: true);
            var twinpack = new TwinpackService(new PackageServerCollection { AppDepServer() }, CreateAutomationInterface(ai, config), config);

            await twinpack.UpdatePackagesAsync(options: new TwinpackService.UpdatePackageOptions
            {
                SkipInstall = skipInstall,
                SkipDownload = skipDownload,
                DownloadPath = CacheWithApp()
            });

            AssertInstalls(expected);
            AssertReferenced(config, "App", "Dep");
        }

        /// <summary>
        /// Add, restore and update refuse a configuration with modules before they touch anything.
        /// Only set-version walks into modules (see the SetPackageVersionAsync rows below).
        /// </summary>
        [DataTestMethod]
        [DataRow("add",     NoAi)]
        [DataRow("add",     Headless)]
        [DataRow("add",     Mocked)]
        [DataRow("restore", NoAi)]
        [DataRow("restore", Headless)]
        [DataRow("restore", Mocked)]
        [DataRow("update",  NoAi)]
        [DataRow("update",  Headless)]
        [DataRow("update",  Mocked)]
        public async Task Operation_WithModules_IsRefused(string operation, string ai)
        {
            var config = AppDepConfig(referenced: true);
            config.Modules = new List<string> { "ModA" };
            var twinpack = new TwinpackService(new PackageServerCollection { AppDepServer() }, CreateAutomationInterface(ai, config), config);
            var cache = CacheWithApp();

            Func<Task> act;
            switch (operation)
            {
                case "add":
                    act = () => twinpack.AddPackagesAsync(new List<PackageItem> { AppRequest() }, new TwinpackService.AddPackageOptions { DownloadPath = cache });
                    break;
                case "restore":
                    act = () => twinpack.RestorePackagesAsync(new TwinpackService.RestorePackageOptions { DownloadPath = cache });
                    break;
                default:
                    act = () => twinpack.UpdatePackagesAsync(options: new TwinpackService.UpdatePackageOptions { DownloadPath = cache });
                    break;
            }

            var error = await Assert.ThrowsExceptionAsync<NotSupportedException>(act);
            Assert.AreEqual("Modules are not supported", error.Message);
            AssertInstalls("");
        }

        #endregion

        #region SetPackageVersionAsync

        // ZPlatform references the framework library ZCore 1.6.0.1 and the non-framework library Struckig.
        // 1.6.0.29 is published for ZCore, 1.6.0.30 is not. SetPackageVersionOptions has no install or
        // download option, so the second axis is "is the requested framework version published".
        // TwinpackService.cs:1711 adds with no options, :1712 adds with SkipDownload = true.

        [DataTestMethod]
        //      interface scope     sync   purge  version      expected InstallPackageAsync calls
        [DataRow(NoAi,     TopLevel, false, false, "1.6.0.29", "")]
        [DataRow(Headless, TopLevel, false, false, "1.6.0.29", "")]
        [DataRow(Mocked,   TopLevel, false, false, "1.6.0.29", "")]
        [DataRow(NoAi,     Modules,  false, false, "1.6.0.29", "")]
        [DataRow(Headless, Modules,  false, false, "1.6.0.29", "")]
        [DataRow(Mocked,   Modules,  false, false, "1.6.0.29", "")]
        // NoAi + TopLevel + sync: see SetPackageVersionAsync_TopLevel_NoAutomationInterface_SyncFrameworkPackages
        [DataRow(Headless, TopLevel, true,  false, "1.6.0.29", "")]
        [DataRow(Headless, TopLevel, true,  false, "1.6.0.30", "")]
        [DataRow(Headless, TopLevel, true,  true,  "1.6.0.29", "")]
        [DataRow(Mocked,   TopLevel, true,  false, "1.6.0.29", "ZCore")]
        [DataRow(Mocked,   TopLevel, true,  false, "1.6.0.30", "")]
        [DataRow(Mocked,   TopLevel, true,  true,  "1.6.0.29", "Struckig,ZCore")]
        // Modules always get a fresh AutomationInterfaceHeadless (TwinpackService.cs:1514, :1519, :1709),
        // whatever interface the caller passed, so they never install.
        [DataRow(NoAi,     Modules,  true,  false, "1.6.0.29", "")]
        [DataRow(NoAi,     Modules,  true,  false, "1.6.0.30", "")]
        [DataRow(NoAi,     Modules,  true,  true,  "1.6.0.29", "")]
        [DataRow(Headless, Modules,  true,  false, "1.6.0.29", "")]
        [DataRow(Headless, Modules,  true,  false, "1.6.0.30", "")]
        [DataRow(Headless, Modules,  true,  true,  "1.6.0.29", "")]
        [DataRow(Mocked,   Modules,  true,  false, "1.6.0.29", "")]
        [DataRow(Mocked,   Modules,  true,  false, "1.6.0.30", "")]
        [DataRow(Mocked,   Modules,  true,  true,  "1.6.0.29", "")]
        public async Task SetPackageVersionAsync_InstallIntent(string ai, string scope, bool sync, bool purge, string version, string expected)
        {
            var config = SetVersionWorkspace(scope);
            var twinpack = new TwinpackService(new PackageServerCollection { FrameworkServer() }, CreateAutomationInterface(ai, config), config);

            await twinpack.SetPackageVersionAsync(version, SetVersionOptions(sync, purge));

            AssertInstalls(expected);
        }

        /// <summary>
        /// Known bug, not expected behaviour: with no automation interface, set-version with
        /// SyncFrameworkPackages calls <c>automationInterface.SaveAllAsync()</c> on null
        /// (TwinpackService.cs:1713). zkmake reaches it with <c>set-version --skip-plc-update</c>.
        /// The test expects the fixed behaviour and reports Inconclusive while the bug is present.
        /// WP1.2 fixes it, and then this test passes without changes.
        /// </summary>
        [DataTestMethod]
        [DataRow(false, "1.6.0.29")]
        [DataRow(false, "1.6.0.30")]
        [DataRow(true,  "1.6.0.29")]
        public async Task SetPackageVersionAsync_TopLevel_NoAutomationInterface_SyncFrameworkPackages(bool purge, string version)
        {
            var config = SetVersionWorkspace(TopLevel);
            var twinpack = new TwinpackService(new PackageServerCollection { FrameworkServer() }, automationInterface: null, config: config);

            try
            {
                await twinpack.SetPackageVersionAsync(version, SetVersionOptions(sync: true, purge: purge));
            }
            catch (NullReferenceException ex)
            {
                AssertInstalls("");
                Assert.Inconclusive("Known bug (fixed in WP1.2): set-version with SyncFrameworkPackages and no automation interface " +
                    "throws a NullReferenceException at TwinpackService.cs:1713. " + ex.Message);
            }

            AssertInstalls("");
        }

        #endregion

        #region helpers

        void AssertInstalls(string expected)
        {
            Assert.AreEqual(expected, Sorted(_installCalls), "InstallPackageAsync calls on the given automation interface");
            Assert.AreEqual(expected, Sorted(_log.Logs.Select(x => InstallLine.Match(x)).Where(x => x.Success).Select(x => x.Groups[1].Value)),
                "[install] lines logged before every InstallPackageAsync call");
        }

        static void AssertReferenced(Config config, params string[] names)
        {
            CollectionAssert.AreEquivalent(names, config.Projects[0].Plcs[0].Packages.Select(x => x.Name).ToList());
        }

        static string Sorted(IEnumerable<string> names)
        {
            return string.Join(",", names.OrderBy(x => x, StringComparer.Ordinal));
        }

        IAutomationInterface CreateAutomationInterface(string kind, Config config)
        {
            switch (kind)
            {
                case NoAi:
                    return null;
                case Headless:
                    return new RecordingHeadless(config, _installCalls);
                case Mocked:
                    // Loose, like the existing tests: a mock of the interface, not of the AutomationInterface class.
                    var automation = new Mock<IAutomationInterface>();
                    automation.Setup(x => x.CloseAllPackageRelatedWindowsAsync(It.IsAny<List<PackageItem>>())).Returns(Task.CompletedTask);
                    automation.Setup(x => x.AddPackageAsync(It.IsAny<PackageItem>())).Returns(Task.CompletedTask);
                    automation.Setup(x => x.RemoveAllPackagesAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
                    automation.Setup(x => x.SetPackageVersionAsync(It.IsAny<ConfigPlcProject>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                    automation.Setup(x => x.SaveAllAsync()).Returns(Task.CompletedTask);
                    automation.Setup(x => x.IsPackageInstalledAsync(It.IsAny<PackageItem>())).ReturnsAsync(false);
                    automation.Setup(x => x.InstallPackageAsync(It.IsAny<PackageItem>(), It.IsAny<string>()))
                        .Callback<PackageItem, string>((package, cachePath) => _installCalls.Add(package.PackageVersion.Name))
                        .Returns(Task.CompletedTask);
                    return automation.Object;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        sealed class RecordingHeadless : AutomationInterfaceHeadless
        {
            readonly List<string> _calls;

            public RecordingHeadless(Config config, List<string> calls) : base(config)
            {
                _calls = calls;
            }

            public override Task InstallPackageAsync(PackageItem package, string cachePath = null)
            {
                _calls.Add(package.PackageVersion.Name);
                return base.InstallPackageAsync(package, cachePath);
            }

            public static void UseLocalRepository(List<XElement> libraries) => LocalRepository = libraries;
        }

        static PackageServerMock AppDepServer()
        {
            return new PackageServerMock
            {
                Connected = true,
                PackageVersionItems = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse
                    {
                        Name = "App", Title = "App", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1", DistributorName = Vendor,
                        Dependencies = new List<PackageVersionGetResponse>
                        {
                            new PackageVersionGetResponse { Name = "Dep", Version = "1.0.0.0", DistributorName = Vendor }
                        }
                    },
                    new PackageVersionGetResponse
                    {
                        Name = "Dep", Title = "Dep", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1", DistributorName = Vendor,
                        Dependencies = new List<PackageVersionGetResponse>()
                    }
                }
            };
        }

        static ConfigPlcPackage AppRow()
        {
            return new ConfigPlcPackage { Name = "App", Version = "1.0.0.0", Branch = "main", Configuration = "Release", Target = "TC3.1", DistributorName = Vendor };
        }

        static PackageItem AppRequest()
        {
            return new PackageItem { ProjectName = "P", PlcName = "PLC", Config = AppRow() };
        }

        Config AppDepConfig(bool referenced)
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
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject
                            {
                                Name = "PLC",
                                ProjectName = "P",
                                FilePath = plcproj,
                                Packages = referenced ? new List<ConfigPlcPackage> { AppRow() } : new List<ConfigPlcPackage>()
                            }
                        }
                    }
                }
            };
        }

        string CacheWithApp()
        {
            var cache = Path.Combine(_directory, "libraries");
            var target = Path.Combine(cache, "TC3.1");
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "App_1.0.0.0.library"), "app");
            return cache;
        }

        static TwinpackService.SetPackageVersionOptions SetVersionOptions(bool sync, bool purge)
        {
            return new TwinpackService.SetPackageVersionOptions
            {
                SyncFrameworkPackages = sync,
                PurgePackages = purge,
                PreferredFrameworkBranch = "release/1.6",
                PreferredFrameworkConfiguration = "Snapshot",
                PreferredFrameworkTarget = "TC3.1"
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

        /// <summary>
        /// Top level: ZPlatform is a PLC of the loaded configuration. Modules: the loaded configuration only
        /// lists the module ModA, and ZPlatform lives in ModA's own configuration, which TwinpackService loads
        /// from the working directory.
        /// </summary>
        Config SetVersionWorkspace(string scope)
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

        #endregion
    }
}
