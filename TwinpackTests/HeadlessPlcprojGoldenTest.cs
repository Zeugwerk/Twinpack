using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    /// <summary>
    /// WP0.4 golden-file tests for the .plcproj edits of <see cref="AutomationInterfaceHeadless"/>. Every
    /// scenario starts from one of the inputs in assets\HeadlessPlcproj (UTF-8 with BOM, CRLF, two-space
    /// indent, as TwinCAT writes it) and compares the result byte for byte with &lt;scenario&gt;.expected.plcproj.
    /// Input.plcproj is a typical project, Minimal.plcproj has no placeholder references and no
    /// Title/Company/ProjectVersion, WithParameters.plcproj holds parameters in the form TwinCAT 4024 writes.
    ///
    /// The expected files record today's output, not a reviewed ideal. To accept a deliberate change, run the
    /// tests with TWINPACK_UPDATE_GOLDEN=1: they rewrite the expected files in the source tree and report
    /// Inconclusive. Review the diff before committing it.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class HeadlessPlcprojGoldenTest
    {
        const string UpdateVariable = "TWINPACK_UPDATE_GOLDEN";
        const string ProjectName = "TestProject";
        const string PlcName = "Plc1";
        static readonly string AssetFolder = Path.Combine("assets", "HeadlessPlcproj");

        // Scenarios that fail today. They report Inconclusive while the bug is present; the fix removes the
        // entry and adds the expected file.
        static readonly Dictionary<string, Type> KnownBugs = new Dictionary<string, Type>
        {
        };

        string _directory;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "twinpack-golden-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);

            // Headless reads the machine's library index into a static field. With an empty index the
            // output does not depend on the libraries installed on the machine running the tests.
            InstalledLibraries.Use(new List<XElement>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            InstalledLibraries.Use(null);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [DataTestMethod]
        [DataRow("AddNewPlaceholder")]
        [DataRow("AddReplacesPlaceholder")]
        [DataRow("AddKeepsUnmanagedChildren")]
        [DataRow("AddWithNamespaceOptionsAndParameters")]
        [DataRow("AddAsLibraryReference")]
        [DataRow("RemovePlaceholder")]
        [DataRow("RemoveLibraryReference")]
        [DataRow("RemoveAll")]
        [DataRow("SetVersion")]
        [DataRow("AddAsLibraryReferenceWithoutPlaceholders")]
        [DataRow("SetVersionAddsMissingProperties")]
        [DataRow("SetVersionWithoutDistributor")]
        [DataRow("ReAddReplacesParameters")]
        [DataRow("ReAddWithoutParameters")]
        public async Task HeadlessEdit_MatchesGoldenFile(string scenario)
        {
            var path = Path.Combine(_directory, PlcName, PlcName + ".plcproj");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(Path.Combine(AssetFolder, InputFor(scenario)), path);

            var plc = new ConfigPlcProject { Name = PlcName, ProjectName = ProjectName, FilePath = path };
            var config = new Config
            {
                FilePath = Path.Combine(_directory, ".Zeugwerk", "config.json"),
                WorkingDirectory = _directory,
                Projects = new List<ConfigProject> { new ConfigProject { Name = ProjectName, Plcs = new List<ConfigPlcProject> { plc } } }
            };
            var headless = new AutomationInterfaceHeadless(config);

            try
            {
                await ApplyAsync(scenario, headless, plc);
            }
            catch (Exception ex) when (KnownBugs.TryGetValue(scenario, out var bug) && bug.IsInstanceOfType(ex))
            {
                Assert.Inconclusive($"Known bug: {ex.GetType().Name}: {ex.Message}");
            }

            var actual = File.ReadAllBytes(path);
            var fileName = scenario + ".expected.plcproj";
            if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
            {
                File.WriteAllBytes(Path.Combine(SourceAssetFolder(), fileName), actual);
                Assert.Inconclusive($"{fileName} rewritten from the current output");
            }

            var expectedPath = Path.Combine(AssetFolder, fileName);
            Assert.IsTrue(File.Exists(expectedPath), $"{expectedPath} is missing. Run the tests with {UpdateVariable}=1 to create it.");

            var expected = File.ReadAllBytes(expectedPath);
            // Text first, so a failure shows a readable difference. Bytes second, for BOM and line endings.
            Assert.AreEqual(Encoding.UTF8.GetString(expected), Encoding.UTF8.GetString(actual));
            CollectionAssert.AreEqual(expected, actual, "same text, different bytes (BOM or line endings)");
        }

        static async Task ApplyAsync(string scenario, AutomationInterfaceHeadless headless, ConfigPlcProject plc)
        {
            switch (scenario)
            {
                case "AddNewPlaceholder":
                    await headless.AddPackageAsync(Library("Struckig", "0.12.0.0", "Stefan Besler"));
                    break;

                case "AddReplacesPlaceholder":
                    await headless.AddPackageAsync(Library("ZCore", "1.6.0.29", "Zeugwerk GmbH"));
                    break;

                case "AddKeepsUnmanagedChildren":
                    // Tc3_Module carries SystemLibrary and ResolverGuid, which the writer does not emit itself.
                    await headless.AddPackageAsync(Library("Tc3_Module", "3.3.24.0", "Beckhoff Automation GmbH"));
                    break;

                case "AddWithNamespaceOptionsAndParameters":
                {
                    var package = Library("ZAux", "1.0.0.0", "Zeugwerk GmbH");
                    package.Config.Namespace = "Aux";
                    package.Config.Options = new AddPlcLibraryOptions { Optional = true, HideWhenReferencedAsDependency = true, PublishSymbolsInContainer = true, QualifiedOnly = true };
                    package.Config.Parameters = new Dictionary<string, string> { { "GVL_Param.MaxAxes", "8" } };
                    await headless.AddPackageAsync(package);
                    break;
                }

                case "AddAsLibraryReference":
                {
                    var package = Library("Tc2_Utilities", "3.3.59.0", "Beckhoff Automation GmbH");
                    package.Config.Options = new AddPlcLibraryOptions { LibraryReference = true };
                    await headless.AddPackageAsync(package);
                    break;
                }

                case "RemovePlaceholder":
                    await headless.RemovePackageAsync(Library("Tc2_Standard", "3.4.5.0", "Beckhoff Automation GmbH"));
                    break;

                case "RemoveLibraryReference":
                    await headless.RemovePackageAsync(Library("Tc2_System", "3.4.26.0", "Beckhoff Automation GmbH"));
                    break;

                case "RemoveAll":
                    await headless.RemoveAllPackagesAsync(ProjectName, PlcName);
                    break;

                case "SetVersion":
                    plc.Version = "1.2.3.4";
                    plc.DistributorName = "Zeugwerk GmbH";
                    await headless.SetPackageVersionAsync(plc);
                    break;

                case "AddAsLibraryReferenceWithoutPlaceholders":
                {
                    var package = Library("Tc2_Utilities", "3.3.59.0", "Beckhoff Automation GmbH");
                    package.Config.Options = new AddPlcLibraryOptions { LibraryReference = true };
                    await headless.AddPackageAsync(package);
                    break;
                }

                case "SetVersionAddsMissingProperties":
                    plc.Version = "1.2.3.4";
                    plc.DistributorName = "Zeugwerk GmbH";
                    await headless.SetPackageVersionAsync(plc);
                    break;

                case "SetVersionWithoutDistributor":
                    // No "type" in config.json, so PlcType is FrameworkLibrary.
                    plc.Version = "1.2.3.4";
                    await headless.SetPackageVersionAsync(plc);
                    break;

                case "ReAddReplacesParameters":
                {
                    var package = Library("TcUnit", "1.3.1.0", "www.tcunit.org");
                    package.Config.Parameters = new Dictionary<string, string> { { "GVL_PARAM_TCUNIT.MAXNUMBEROFTESTSUITES", "500" } };
                    await headless.AddPackageAsync(package);
                    break;
                }

                case "ReAddWithoutParameters":
                    await headless.AddPackageAsync(Library("TcUnit", "1.3.1.0", "www.tcunit.org"));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
            }
        }

        static string InputFor(string scenario)
        {
            switch (scenario)
            {
                case "AddAsLibraryReferenceWithoutPlaceholders":
                case "SetVersionAddsMissingProperties":
                case "SetVersionWithoutDistributor":
                    return "Minimal.plcproj";
                case "ReAddReplacesParameters":
                case "ReAddWithoutParameters":
                    return "WithParameters.plcproj";
                default:
                    return "Input.plcproj";
            }
        }

        static PackageItem Library(string name, string version, string distributor)
        {
            return new PackageItem
            {
                ProjectName = ProjectName,
                PlcName = PlcName,
                PackageVersion = new PackageVersionGetResponse { Name = name, Title = name, Version = version, DistributorName = distributor },
                Config = new ConfigPlcPackage { Name = name, Version = version, DistributorName = distributor }
            };
        }

        static string SourceAssetFolder([CallerFilePath] string sourceFile = null)
        {
            return Path.Combine(Path.GetDirectoryName(sourceFile), "assets", "HeadlessPlcproj");
        }

        sealed class InstalledLibraries : AutomationInterfaceHeadless
        {
            InstalledLibraries() : base(new Config()) { }

            public static void Use(List<XElement> libraries) => LocalRepository = libraries;
        }
    }
}