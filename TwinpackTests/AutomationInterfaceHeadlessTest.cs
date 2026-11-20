using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    [TestClass]
    public class AutomationInterfaceHeadlessTest
    {
        class RepositoryHeadless : AutomationInterfaceHeadless
        {
            public RepositoryHeadless(Config config) : base(config)
            {
            }

            public static void UseRepository(List<XElement> libraries)
            {
                LocalRepository = libraries;
            }
        }

        [TestMethod]
        public async Task AddPackageUsesTheLibraryFileVersionWhenNothingIsInstalled()
        {
            var directory = Path.Combine(Path.GetTempPath(), "twinpack-libfile-" + Guid.NewGuid().ToString("N"));
            try
            {
                var plcproj = Path.Combine(directory, "Demo", "Demo.plcproj");
                Directory.CreateDirectory(Path.GetDirectoryName(plcproj));
                File.WriteAllText(plcproj,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<Project DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                    "<PropertyGroup><Name>Demo</Name></PropertyGroup>" +
                    "</Project>");

                var library = Path.Combine(directory, "DemoLib.library");
                WriteLibrary(library, "DemoLib", "3.4.7.0");

                var config = new Config
                {
                    FilePath = Path.Combine(directory, ".Zeugwerk", "config.json"),
                    WorkingDirectory = directory,
                    Projects = new List<ConfigProject>
                    {
                        new ConfigProject
                        {
                            Name = "Demo",
                            Plcs = new List<ConfigPlcProject>
                            {
                                new ConfigPlcProject
                                {
                                    Name = "Demo",
                                    FilePath = plcproj
                                }
                            }
                        }
                    }
                };

                var package = new PackageItem
                {
                    ProjectName = "Demo",
                    PlcName = "Demo",
                    Config = new ConfigPlcPackage { Name = "DemoLib", Version = "3.4.7", DistributorName = "Zeugwerk" },
                    PackageVersion = new PackageVersionGetResponse
                    {
                        Name = "DemoLib",
                        Title = "DemoLib",
                        Version = "3.4.7",
                        DistributorName = "Zeugwerk",
                        LibraryFiles = new List<string> { library }
                    }
                };

                RepositoryHeadless.UseRepository(new List<XElement>());
                await new RepositoryHeadless(config).AddPackageAsync(package);

                var written = File.ReadAllText(plcproj);
                StringAssert.Contains(written, "DemoLib, 3.4.7.0 (Zeugwerk)");
                StringAssert.Contains(written, "DemoLib, * (Zeugwerk)");
                Assert.AreEqual("3.4.7", package.PackageVersion.Version);
                Assert.AreEqual("3.4.7", package.Config.Version);
            }
            finally
            {
                RepositoryHeadless.UseRepository(null);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        static void WriteLibrary(string path, string title, string version)
        {
            using (var stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("projectinformations.auxiliary");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(
                        "<Project><Properties>" +
                        "<Title>" + title + "</Title>" +
                        "<Version>" + version + "</Version>" +
                        "<Company>Zeugwerk</Company>" +
                        "</Properties></Project>");
                }
            }
        }
    }
}
