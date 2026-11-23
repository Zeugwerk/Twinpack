using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    [TestClass]
    public class PlcProjectFileTest
    {
        // Four-space indent. The default writer uses two spaces, so a re-indent cannot pass unnoticed.
        const string ModuleReference =
            "        <PlaceholderReference Include=\"Tc3_Module\">\n" +
            "            <DefaultResolution>Tc3_Module, * (Beckhoff Automation GmbH)</DefaultResolution>\n" +
            "            <Namespace>Tc3_Module</Namespace>\n" +
            "            <SystemLibrary>true</SystemLibrary>\n" +
            "            <RepositoryId>abc</RepositoryId>\n" +
            "        </PlaceholderReference>";

        const string ProjectXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Project DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\n" +
            "    <PropertyGroup>\n" +
            "        <Name>Plc1</Name>\n" +
            "    </PropertyGroup>\n" +
            "    <ItemGroup>\n" +
            "        <Folder Include=\"DUTs\" />\n" +
            "    </ItemGroup>\n" +
            "    <ItemGroup>\n" +
            ModuleReference + "\n" +
            "        <PlaceholderReference Include=\"Tc2_System\">\n" +
            "            <DefaultResolution>Tc2_System, * (Beckhoff Automation GmbH)</DefaultResolution>\n" +
            "            <Namespace>Tc2_System</Namespace>\n" +
            "            <SystemLibrary>true</SystemLibrary>\n" +
            "        </PlaceholderReference>\n" +
            "    </ItemGroup>\n" +
            "    <ItemGroup>\n" +
            "        <PlaceholderResolution Include=\"Tc3_Module\">\n" +
            "            <Resolution>Tc3_Module, 3.3.23.0 (Beckhoff Automation GmbH)</Resolution>\n" +
            "        </PlaceholderResolution>\n" +
            "    </ItemGroup>\n" +
            "</Project>\n";

        [TestMethod]
        public async Task RemoveOfMissingLibrary_KeepsEncodingAndWhitespace()
        {
            await WithProject(utf8Bom: false, newline: "\r\n", async (automation, path) =>
            {
                var before = File.ReadAllBytes(path);

                await automation.RemovePackageAsync(Library("NotInProject", "1.0.0.0"));

                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            });
        }

        [TestMethod]
        public async Task RemoveOfMissingLibrary_KeepsExistingBom()
        {
            await WithProject(utf8Bom: true, newline: "\n", async (automation, path) =>
            {
                var before = File.ReadAllBytes(path);

                await automation.RemovePackageAsync(Library("NotInProject", "1.0.0.0"));

                CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
                Assert.IsTrue(File.ReadAllBytes(path).Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()));
            });
        }

        [TestMethod]
        public async Task Rewrite_KeepsUnmanagedChildren()
        {
            await WithProject(utf8Bom: false, newline: "\r\n", async (automation, path) =>
            {
                await automation.AddPackageAsync(Library("Tc3_Module", "3.3.24.0", "Beckhoff Automation GmbH"));

                var text = File.ReadAllText(path);
                var reference = ReferenceElement(text, "Tc3_Module");
                Assert.AreEqual("true", reference.Element(ConfigPlcProjectFactory.TcNs + "SystemLibrary")?.Value);
                Assert.AreEqual("abc", reference.Element(ConfigPlcProjectFactory.TcNs + "RepositoryId")?.Value);
                Assert.AreEqual("*", VersionOf(reference));
                StringAssert.Contains(ResolutionOf(text, "Tc3_Module"), "3.3.24.0");
                StringAssert.Contains(text, "        <PlaceholderReference Include=\"Tc2_System\">");
                StringAssert.Contains(text, "            <SystemLibrary>true</SystemLibrary>\n".Replace("\n", "\r\n") +
                    "        </PlaceholderReference>");
            });
        }

        [TestMethod]
        public async Task NewReference_DoesNotInventSystemLibrary()
        {
            await WithProject(utf8Bom: false, newline: "\r\n", async (automation, path) =>
            {
                await automation.AddPackageAsync(Library("TwinpackIssue183", "0.0.3"));

                var text = File.ReadAllText(path);
                var reference = ReferenceElement(text, "TwinpackIssue183");
                Assert.IsNull(reference.Element(ConfigPlcProjectFactory.TcNs + "SystemLibrary"));
                Assert.AreEqual("*", VersionOf(reference));
                StringAssert.Contains(ResolutionOf(text, "TwinpackIssue183"), "0.0.3");
            });
        }

        [TestMethod]
        public async Task AddReference_DoesNotReindentOrAddBom()
        {
            await WithProject(utf8Bom: false, newline: "\r\n", async (automation, path) =>
            {
                await automation.AddPackageAsync(Library("TwinpackIssue183", "0.0.3"));

                var text = File.ReadAllText(path);
                StringAssert.Contains(text, ModuleReference.Replace("\n", "\r\n"));
                StringAssert.Contains(text, "TwinpackIssue183");
                Assert.IsFalse(HasUtf8Bom(path));
                StringAssert.Contains(text, "        <Folder Include=\"DUTs\" />");
            });
        }

        /// <summary>
        /// A feed without the tp-library-version tag reports a library versioned 3.3.24.0 as 3.3.24.
        /// TwinCAT only resolves the reference under the version it registered, so headless takes the
        /// installed spelling, and hands it back so config.json records it too.
        /// </summary>
        [DataTestMethod]
        [DataRow("3.3.24", "3.3.24.0", "3.3.24.0")]
        [DataRow("3.3.24-feat-ci", "3.3.24.0", "3.3.24.0-feat-ci")]
        [DataRow("3.3.24.0", "3.3.24", "3.3.24")]
        [DataRow("3.3.24", "3.3.25.0", "3.3.24")]
        [DoNotParallelize]
        public async Task AddReference_UsesTheVersionTheLibraryIsInstalledUnder(string requested, string installed, string expected)
        {
            await WithInstalledLibraries(new[] { InstalledLibrary("Tc3_Module", "Beckhoff Automation GmbH", installed) }, async () =>
            {
                await WithProject(utf8Bom: false, newline: "\r\n", async (automation, path) =>
                {
                    var package = Library("Tc3_Module", requested, "Beckhoff Automation GmbH");
                    await automation.AddPackageAsync(package);

                    Assert.AreEqual(expected, package.PackageVersion.Version);
                    Assert.AreEqual($"Tc3_Module, {AutomationInterface.TwinCATLibraryVersion(expected)} (Beckhoff Automation GmbH)",
                        ResolutionOf(File.ReadAllText(path), "Tc3_Module"));
                });
            });
        }

        /// <summary>
        /// TwinCAT allows a library to be versioned 1.2.3 and another 1.2.3.0. When the exact version is
        /// installed it is the one referenced, not the other spelling.
        /// </summary>
        [TestMethod]
        [DoNotParallelize]
        public async Task AddReference_PrefersTheExactlyInstalledVersion()
        {
            var installed = new[]
            {
                InstalledLibrary("MyLib", "Vendor", "1.2.3.0"),
                InstalledLibrary("MyLib", "Vendor", "1.2.3")
            };

            await WithInstalledLibraries(installed, async () =>
            {
                await WithProject(utf8Bom: false, newline: "\r\n", async (automation, path) =>
                {
                    var package = Library("MyLib", "1.2.3");
                    await automation.AddPackageAsync(package);

                    Assert.AreEqual("1.2.3", package.PackageVersion.Version);
                    Assert.AreEqual("MyLib, 1.2.3 (Vendor)", ResolutionOf(File.ReadAllText(path), "MyLib"));
                });
            });
        }

        static XElement InstalledLibrary(string title, string company, string version)
        {
            return new XElement("Library",
                new XAttribute("Title", title),
                new XAttribute("Company", company),
                new XAttribute("Version", version),
                new XAttribute("DefaultNamespace", title));
        }

        static async Task WithInstalledLibraries(XElement[] libraries, Func<Task> act)
        {
            InstalledLibraries.Use(libraries.ToList());
            try
            {
                await act();
            }
            finally
            {
                // the next test loads the machine's own library index again
                InstalledLibraries.Use(null);
            }
        }

        sealed class InstalledLibraries : AutomationInterfaceHeadless
        {
            InstalledLibraries() : base(new Config()) { }

            public static void Use(System.Collections.Generic.List<XElement> libraries) => LocalRepository = libraries;
        }

        static async Task WithProject(bool utf8Bom, string newline, Func<AutomationInterfaceHeadless, string, Task> act)
        {
            var directory = Path.Combine(Path.GetTempPath(), "twinpack-plcproj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "Plc1.plcproj");
            try
            {
                var text = ProjectXml.Replace("\n", newline);
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: utf8Bom);
                File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());

                var config = new Config
                {
                    FilePath = Path.Combine(directory, "config.json"),
                    WorkingDirectory = directory,
                    Projects = new System.Collections.Generic.List<ConfigProject>
                    {
                        new ConfigProject
                        {
                            Name = "TestProject",
                            Plcs = new System.Collections.Generic.List<ConfigPlcProject>
                            {
                                new ConfigPlcProject { Name = "Plc1", FilePath = path }
                            }
                        }
                    }
                };

                await act(new AutomationInterfaceHeadless(config), path);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        static PackageItem Library(string title, string version, string distributor = "Vendor")
        {
            return new PackageItem
            {
                ProjectName = "TestProject",
                PlcName = "Plc1",
                PackageVersion = new PackageVersionGetResponse
                {
                    Name = title,
                    Title = title,
                    Version = version,
                    DistributorName = distributor
                },
                Config = new ConfigPlcPackage
                {
                    Name = title,
                    Version = version,
                    DistributorName = distributor
                }
            };
        }

        static XElement ReferenceElement(string plcproj, string title)
        {
            return XDocument.Parse(plcproj)
                .Descendants(ConfigPlcProjectFactory.TcNs + "PlaceholderReference")
                .Single(element => element.Attribute("Include")?.Value == title);
        }

        static string ResolutionOf(string plcproj, string title)
        {
            return XDocument.Parse(plcproj)
                .Descendants(ConfigPlcProjectFactory.TcNs + "PlaceholderResolution")
                .Single(element => element.Attribute("Include")?.Value == title)
                .Element(ConfigPlcProjectFactory.TcNs + "Resolution")?.Value ?? "";
        }

        static string VersionOf(XElement reference)
        {
            var resolution = reference.Element(ConfigPlcProjectFactory.TcNs + "DefaultResolution")?.Value ?? "";
            var comma = resolution.IndexOf(',');
            var open = resolution.IndexOf('(');
            if (comma < 0 || open < 0)
                return null;
            return resolution.Substring(comma + 1, open - comma - 1).Trim();
        }

        static bool HasUtf8Bom(string path)
        {
            var preamble = Encoding.UTF8.GetPreamble();
            var bytes = File.ReadAllBytes(path);
            return bytes.Length >= preamble.Length && bytes.Take(preamble.Length).SequenceEqual(preamble);
        }
    }
}
