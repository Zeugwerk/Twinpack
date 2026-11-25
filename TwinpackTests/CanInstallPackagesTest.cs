using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    /// <summary>
    /// TwinpackService asks <see cref="AutomationInterface.CanInstallPackages"/> whether to install, not the
    /// interface's type. The install matrix for today's interfaces is in InstallIntentCharacterizationTest.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class CanInstallPackagesTest
    {
        string _directory;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "twinpack-can-install-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            InstallingHeadless.UseLocalRepository(new List<XElement>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            InstallingHeadless.UseLocalRepository(null);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [TestMethod]
        public void Headless_CannotInstall()
        {
            Assert.IsFalse(new AutomationInterfaceHeadless(new Config()).CanInstallPackages);
        }

        [DataTestMethod]
        [DataRow(false, false, "App,Dep")]
        [DataRow(false, true,  "App")]
        [DataRow(true,  false, "")]
        public async Task InterfaceThatCanInstall_IsAskedToInstall(bool skipInstall, bool skipDownload, string expected)
        {
            var plcproj = Path.Combine(_directory, "P", "PLC.plcproj");
            Directory.CreateDirectory(Path.GetDirectoryName(plcproj));
            File.WriteAllText(plcproj,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<Project DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                "<PropertyGroup><Name>PLC</Name></PropertyGroup>" +
                "</Project>");
            var cache = Path.Combine(_directory, "libraries", "TC3.1");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "App_1.0.0.0.library"), "app");

            var config = new Config
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
            var server = new PackageServerMock
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
            var automation = new InstallingHeadless(config);
            var twinpack = new TwinpackService(new PackageServerCollection { server }, automation, config);

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
                new TwinpackService.AddPackageOptions { SkipInstall = skipInstall, SkipDownload = skipDownload, DownloadPath = Path.Combine(_directory, "libraries") });

            Assert.AreEqual(expected, string.Join(",", automation.Installed.OrderBy(x => x, StringComparer.Ordinal)));
        }

        // A headless interface that says it can install, standing in for one that has an installer.
        sealed class InstallingHeadless : AutomationInterfaceHeadless
        {
            public InstallingHeadless(Config config) : base(config) { }

            public List<string> Installed { get; } = new List<string>();

            public override bool CanInstallPackages => true;

            public override Task InstallPackageAsync(PackageItem package, string cachePath = null)
            {
                Installed.Add(package.PackageVersion.Name);
                return Task.CompletedTask;
            }

            public static void UseLocalRepository(List<XElement> libraries) => LocalRepository = libraries;
        }
    }
}