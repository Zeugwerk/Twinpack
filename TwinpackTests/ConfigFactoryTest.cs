using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Models;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class ConfigFactoryTest
    {
        [TestMethod]
        public async Task CreateFromSolutionFileWithoutFilterAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");

            Assert.AreEqual(@"assets\TestSolution", config.WorkingDirectory);
            Assert.AreEqual(@"TestSolution.sln", config.Solution);
            Assert.AreEqual(@"assets\TestSolution\.Zeugwerk\config.json", config.FilePath);
            Assert.AreEqual(2, config.Projects.Count);

            var project = config.Projects.Where(x => x.Name == "TestProject").FirstOrDefault();
            Assert.AreEqual(@"TestProject", project?.Name);
            Assert.AreEqual(1, project?.Plcs.Count);

            var plc = project.Plcs.FirstOrDefault();
            Assert.AreEqual(@"Plc1", plc?.Name);
            Assert.AreEqual(@"Plc1", plc?.Title);
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Application, plc?.PlcType);
            Assert.IsNull(plc?.References);
            Assert.AreEqual("1.0.0.0", plc?.Version);
            Assert.AreEqual(3, plc?.SystemReferences.Count);

            Assert.AreEqual("Tc2_Standard", plc.SystemReferences[0].Name);
            Assert.AreEqual("*", plc.SystemReferences[0].Version);
            Assert.AreEqual("Beckhoff Automation GmbH", plc.SystemReferences[0].DistributorName);
            Assert.AreEqual("Tc2_System", plc.SystemReferences[1].Name);
            Assert.AreEqual("*", plc.SystemReferences[1].Version);
            Assert.AreEqual("Beckhoff Automation GmbH", plc.SystemReferences[1].DistributorName);
            Assert.AreEqual("Tc3_Module", plc.SystemReferences[2].Name);
            Assert.AreEqual("*", plc.SystemReferences[2].Version);
            Assert.AreEqual("Beckhoff Automation GmbH", plc.SystemReferences[2].DistributorName);
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public async Task CreateFromSolution_DirectoryNotFound_Async(bool continueWithoutSolution)
        {
            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(async () => await ConfigFactory.CreateFromSolutionFileAsync(@"assets\NoSuchDirectory", continueWithoutSolution));
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public async Task CreateFromSolution_NoFiles_Async(bool continueWithoutSolution)
        {
            if (!Directory.Exists(@"assets\NoSolutionInside"))
                Directory.CreateDirectory(@"assets\NoSolutionInside");

            Assert.IsNull(await ConfigFactory.CreateFromSolutionFileAsync(@"assets\NoSolutionInside", continueWithoutSolution));
        }

        [TestMethod]
        public async Task CreateFromSolutionFileWithFilterAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution",
                plcTypeFilter: new List<ConfigPlcProject.PlcProjectType> { ConfigPlcProject.PlcProjectType.Library });

            Assert.AreEqual(@"assets\TestSolution", config.WorkingDirectory);
            Assert.AreEqual(@"TestSolution.sln", config.Solution);
            Assert.AreEqual(@"assets\TestSolution\.Zeugwerk\config.json", config.FilePath);
            Assert.AreEqual(1, config.Projects.Count);

            var project = config.Projects.FirstOrDefault();
            Assert.AreEqual(@"TestProject2", project?.Name);
            Assert.AreEqual(1, project?.Plcs.Count);

            var plc = project.Plcs.FirstOrDefault();
            Assert.AreEqual(@"TestProject2", plc?.ProjectName);
            Assert.AreEqual(@"PlcLibrary1", plc?.Name);
            Assert.AreEqual(@"PlcLibrary1", plc?.Title);
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Library, plc?.PlcType);
            Assert.IsNull(plc?.References);
            Assert.AreEqual("1.2.3.4", plc?.Version);
            Assert.AreEqual(2, plc?.SystemReferences.Count);

            Assert.AreEqual("Tc2_Standard", plc.SystemReferences[0].Name);
            Assert.AreEqual("*", plc.SystemReferences[0].Version);
            Assert.AreEqual("Beckhoff Automation GmbH", plc.SystemReferences[0].DistributorName);
            Assert.AreEqual("Tc2_System", plc.SystemReferences[1].Name);
            Assert.AreEqual("*", plc.SystemReferences[1].Version);
            Assert.AreEqual("Beckhoff Automation GmbH", plc.SystemReferences[1].DistributorName);
        }

        [TestMethod]
        public async Task CreateFromSolutionFile_SolutionPath_LoadsNamedSolutionAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution\TestSolution.sln");

            Assert.AreEqual(Path.GetFullPath(@"assets\TestSolution"), config.WorkingDirectory);
            Assert.AreEqual(@"TestSolution.sln", config.Solution);
            Assert.AreEqual(Path.Combine(Path.GetFullPath(@"assets\TestSolution"), ".Zeugwerk", "config.json"), config.FilePath);
            Assert.AreEqual(2, config.Projects.Count);
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public async Task CreateFromSolution_SolutionFileNotFound_Async(bool continueWithoutSolution)
        {
            // A named .sln that is not on disk must not fall through to directory discovery — that would
            // quietly load whatever other solution happens to sit next to it.
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () => await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution\NoSuch.sln", continueWithoutSolution));
        }

        [TestMethod]
        public async Task CreateFromSolutionFile_TwoSolutionsInOneFolder_EachLoadsItsOwnProjectsAsync()
        {
            EnsureMultiSolutionFixture();

            var first = await ConfigFactory.CreateFromSolutionFileAsync(Path.Combine(MultiSolutionDir, "First.sln"));
            var second = await ConfigFactory.CreateFromSolutionFileAsync(Path.Combine(MultiSolutionDir, "Second.sln"));

            Assert.AreEqual(@"First.sln", first.Solution);
            Assert.AreEqual(@"TestProject", first.Projects.Single().Name);
            Assert.AreEqual(@"Plc1", first.Projects.Single().Plcs.Single().Name);

            Assert.AreEqual(@"Second.sln", second.Solution);
            Assert.AreEqual(@"TestProject2", second.Projects.Single().Name);
            Assert.AreEqual(@"PlcLibrary1", second.Projects.Single().Plcs.Single().Name);
        }

        [TestMethod]
        public async Task CreateFromSolutionFile_TwoSolutionsInOneFolder_DirectoryStillPicksOneAsync()
        {
            EnsureMultiSolutionFixture();

            var config = await ConfigFactory.CreateFromSolutionFileAsync(MultiSolutionDir);

            // Directory input is unchanged: still first-only, never a merge of both solutions. Which of the
            // two wins is filesystem enumeration order, so only the count is asserted.
            CollectionAssert.Contains(new[] { "First.sln", "Second.sln" }, config.Solution);
            Assert.AreEqual(1, config.Projects.Count);
        }

        private static readonly string MultiSolutionDir = Path.Combine("assets", "MultiSolution");

        /// <summary>
        /// Two solutions side by side in one folder — the shape that first-only discovery cannot express.
        /// Written at test time (like <c>NoSolutionInside</c>) so the fixture needs no csproj copy plumbing;
        /// it points at the TestSolution projects, which are already copied to the output directory.
        /// </summary>
        private static void EnsureMultiSolutionFixture()
        {
            Directory.CreateDirectory(MultiSolutionDir);
            File.WriteAllText(Path.Combine(MultiSolutionDir, "First.sln"),
                SolutionText("TestProject", @"..\TestSolution\TestProject\TestProject.tsproj"));
            File.WriteAllText(Path.Combine(MultiSolutionDir, "Second.sln"),
                SolutionText("TestProject2", @"..\TestSolution\TestProject2\TestProject2.tspproj"));
        }

        private static string SolutionText(string projectName, string projectPath) =>
            "Microsoft Visual Studio Solution File, Format Version 12.00\r\n" +
            "# TcXaeShell Solution File, Format Version 11.00\r\n" +
            "Project(\"{B1E792BE-AA5F-4E3C-8C82-674BF9C0715B}\") = \"" + projectName + "\", \"" + projectPath + "\", \"{FBF99FD5-3861-429A-AA44-FD661631289F}\"\r\n" +
            "EndProject\r\n";

        [TestMethod]
        public void Load_LegacyPublishMetadataFields_AreIgnoredWithoutThrowing()
        {
            // description, authors, display-name, project-url, entitlement, license, license-file,
            // license-tmc-file, icon-file and binary-download-url used to be persisted on a PLC entry.
            // They were removed from ConfigPlcProject; older config.json files that still have them
            // must keep loading fine, just silently dropping those fields (System.Text.Json ignores
            // unmapped members by default), same as the older "repository" field on packages.
            var dir = Path.Combine("assets", "LegacyPublishMetadata");
            Directory.CreateDirectory(Path.Combine(dir, ".Zeugwerk"));
            File.WriteAllText(Path.Combine(dir, ".Zeugwerk", "config.json"), @"{
                ""fileversion"": 1,
                ""solution"": ""Dummy.sln"",
                ""projects"": [
                    {
                        ""name"": ""MyProject"",
                        ""plcs"": [
                            {
                                ""name"": ""MyPlc"",
                                ""version"": ""1.0.0.0"",
                                ""type"": ""Library"",
                                ""distributor-name"": ""My Company"",
                                ""description"": ""Some description"",
                                ""authors"": ""Jane Doe"",
                                ""display-name"": ""My PLC Library"",
                                ""project-url"": ""https://example.com"",
                                ""entitlement"": ""commercial"",
                                ""license"": ""MIT"",
                                ""license-file"": ""LICENSE"",
                                ""license-tmc-file"": ""LICENSE.tmc"",
                                ""icon-file"": ""icon.png"",
                                ""binary-download-url"": ""https://example.com/download"",
                                ""packages"": [
                                    {
                                        ""name"": ""ZCore"",
                                        ""version"": ""1.0.0.0""
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }");

            Config config = null;
            try
            {
                config = ConfigFactory.Load(dir);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }

            Assert.IsNotNull(config);
            Assert.AreEqual(1, config.Projects.Count);

            var plc = config.Projects.Single().Plcs.Single();
            Assert.AreEqual("MyPlc", plc.Name);
            Assert.AreEqual("My Company", plc.DistributorName);
            Assert.AreEqual(1, plc.Packages.Count);
            Assert.AreEqual("ZCore", plc.Packages.Single().Name);
        }

        [TestMethod]
        public void Save_OmitsDefaultBranchTargetAndConfiguration()
        {
            var dir = Path.Combine(Path.GetTempPath(), "twinpack-config-defaults-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var defaults = new ConfigPlcPackage
                {
                    Name = "ZCore",
                    Version = "1.2.0.0",
                    Branch = "Main",
                    Target = "tc3.1",
                    Configuration = "release",
                    DistributorName = "Zeugwerk GmbH"
                };
                var pinned = new ConfigPlcPackage
                {
                    Name = "Struckig",
                    Version = "0.12.0.0",
                    Branch = "release/0.12",
                    Target = "TC3.1.4024.56",
                    Configuration = "Snapshot",
                    DistributorName = "Stefan Besler",
                    Namespace = "Struckig",
                    Parameters = new Dictionary<string, string> { { "gain", "1" } },
                    Options = new AddPlcLibraryOptions { QualifiedOnly = true }
                };
                var unset = new ConfigPlcPackage { Name = "Unset" };
                var config = new Config
                {
                    FilePath = Path.Combine(dir, "config.json"),
                    Solution = "Demo.sln",
                    Projects = new List<ConfigProject>
                    {
                        new ConfigProject
                        {
                            Name = "Demo",
                            Plcs = new List<ConfigPlcProject>
                            {
                                new ConfigPlcProject
                                {
                                    Name = "Plc",
                                    Version = "1.0.0.0",
                                    Type = "Application",
                                    DistributorName = "Zeugwerk GmbH",
                                    Packages = new List<ConfigPlcPackage> { defaults, pinned, unset }
                                }
                            }
                        }
                    }
                };

                ConfigFactory.Save(config);
                var json = File.ReadAllText(config.FilePath);

                Assert.IsFalse(json.Contains("\"branch\": \"main\""));
                Assert.IsFalse(json.Contains("\"branch\": \"Main\""));
                Assert.IsFalse(json.Contains("\"target\": \"TC3.1\""));
                Assert.IsFalse(json.Contains("\"target\": \"tc3.1\""));
                Assert.IsFalse(json.Contains("\"configuration\": \"Release\""));
                Assert.IsFalse(json.Contains("\"configuration\": \"release\""));
                StringAssert.Contains(json, "\"branch\": \"release/0.12\"");
                StringAssert.Contains(json, "\"target\": \"TC3.1.4024.56\"");
                StringAssert.Contains(json, "\"configuration\": \"Snapshot\"");
                StringAssert.Contains(json, "\"namespace\": \"Struckig\"");
                StringAssert.Contains(json, "\"gain\": \"1\"");
                StringAssert.Contains(json, "\"qualified-only\": true");
                StringAssert.Contains(json, "\"version\": null");

                Assert.AreEqual("Main", defaults.Branch);
                Assert.AreEqual("tc3.1", defaults.Target);
                Assert.AreEqual("release", defaults.Configuration);

                var loaded = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var packages = loaded.Projects.Single().Plcs.Single().Packages;

                var loadedDefaults = packages.Single(x => x.Name == "ZCore");
                Assert.AreEqual("1.2.0.0", loadedDefaults.Version);
                Assert.AreEqual(ConfigPlcPackage.DefaultBranch, loadedDefaults.Branch);
                Assert.AreEqual(ConfigPlcPackage.DefaultTarget, loadedDefaults.Target);
                Assert.AreEqual(ConfigPlcPackage.DefaultConfiguration, loadedDefaults.Configuration);
                Assert.AreEqual("Zeugwerk GmbH", loadedDefaults.DistributorName);

                var loadedPinned = packages.Single(x => x.Name == "Struckig");
                Assert.AreEqual("release/0.12", loadedPinned.Branch);
                Assert.AreEqual("TC3.1.4024.56", loadedPinned.Target);
                Assert.AreEqual("Snapshot", loadedPinned.Configuration);
                Assert.AreEqual("Struckig", loadedPinned.Namespace);
                Assert.AreEqual("1", loadedPinned.Parameters["gain"]);
                Assert.IsTrue(loadedPinned.Options.QualifiedOnly);
                Assert.IsFalse(loadedPinned.Options.Optional);

                var loadedUnset = packages.Single(x => x.Name == "Unset");
                Assert.IsNull(loadedUnset.Version);
                Assert.AreEqual(ConfigPlcPackage.DefaultBranch, loadedUnset.Branch);
                Assert.IsNull(loadedUnset.DistributorName);
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public async Task GuessPlcTypeAsyncWithPackageAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");
            var plc = config.Projects.FirstOrDefault().Plcs.FirstOrDefault();

            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Application, ConfigPlcProjectFactory.GuessPlcType(plc));

            plc.Packages.Add(new ConfigPlcPackage { Name = "TcUnit" });
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.UnitTestApplication, ConfigPlcProjectFactory.GuessPlcType(plc));

        }

        [TestMethod]
        public async Task GuessPlcTypeAsyncWithReferenceAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");
            var plc = config.Projects.FirstOrDefault().Plcs.FirstOrDefault();

            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Application, ConfigPlcProjectFactory.GuessPlcType(plc));

            plc.SystemReferences.Add(new ConfigSystemReference { Name = "TcUnit", Version = "*" });
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.UnitTestApplication, ConfigPlcProjectFactory.GuessPlcType(plc));

        }

        [DataTestMethod]
        [DataRow("ZCore")]
        [DataRow("Tc2_Standard")]
        [DataRow("_Internal")]
        [DataRow("Lib3")]
        public void IsUsableNamespaceAcceptsIdentifier(string ns)
        {
            Assert.IsTrue(ConfigPlcPackage.IsUsableNamespace(ns));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("SPT Base Types")]
        [DataRow("TwinCAT.XAE.PLC.Lib.Tc2_System")]
        [DataRow("XY-PLC")]
        [DataRow("3Phase")]
        public void IsUsableNamespaceRejectsEverythingElse(string ns)
        {
            Assert.IsFalse(ConfigPlcPackage.IsUsableNamespace(ns));
        }

        [TestMethod]
        public void Load_MigratesLegacyReferences_SaveOmitsTheMap()
        {
            var dir = Path.Combine(Path.GetTempPath(), "twinpack-legacy-refs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, ".Zeugwerk"));
            File.WriteAllText(Path.Combine(dir, ".Zeugwerk", "config.json"), @"{
                ""fileversion"": 1,
                ""solution"": ""Dummy.sln"",
                ""projects"": [
                    {
                        ""name"": ""MyProject"",
                        ""plcs"": [
                            {
                                ""name"": ""MyPlc"",
                                ""version"": ""1.0.0.0"",
                                ""type"": ""Application"",
                                ""distributor-name"": ""Zeugwerk GmbH"",
                                ""references"": {
                                    ""*"": [ ""Tc2_System=*"", ""Tc2_Standard=3.4.24.0"" ],
                                    ""TC3.1"": [ ""Tc2_System=9.9.9.9"" ],
                                    ""TC3.1.4024.59"": [ ""Tc2_System=1.0.0.0"", ""Tc3_Mc3Base=*"" ],
                                    ""TC3.1.4026"": [ ""Only4026=*"" ]
                                }
                            }
                        ]
                    }
                ]
            }");

            try
            {
                var config = ConfigFactory.Load(dir);
                var plc = config.Projects.Single().Plcs.Single();
                Assert.IsNull(plc.References);
                Assert.AreEqual(4, plc.SystemReferences.Count);

                var system = plc.SystemReferences.Single(x => x.Name == "Tc2_System");
                Assert.AreEqual("*", system.Version);
                Assert.IsNull(system.DistributorName);
                Assert.IsNull(system.When);

                var standard = plc.SystemReferences.Single(x => x.Name == "Tc2_Standard");
                Assert.AreEqual("3.4.24.0", standard.Version);
                Assert.IsNull(standard.When);

                var mc3 = plc.SystemReferences.Single(x => x.Name == "Tc3_Mc3Base");
                Assert.AreEqual("*", mc3.Version);
                CollectionAssert.AreEqual(new[] { "TC3.1.4024" }, mc3.When.Target.ToArray());

                var only4026 = plc.SystemReferences.Single(x => x.Name == "Only4026");
                CollectionAssert.AreEqual(new[] { "TC3.1.4026" }, only4026.When.Target.ToArray());

                ConfigFactory.Save(config);
                var json = File.ReadAllText(config.FilePath);
                Assert.IsTrue(json.Contains("\"system-references\""));
                Assert.IsFalse(json.Contains("\"references\""));

                var reloaded = ConfigFactory.Load(dir);
                var again = reloaded.Projects.Single().Plcs.Single();
                Assert.IsNull(again.References);
                Assert.AreEqual("TC3.1.4024", again.SystemReferences.Single(x => x.Name == "Tc3_Mc3Base").When.Target.Single());
                var systemAgain = again.SystemReferences.Single(x => x.Name == "Tc2_System");
                Assert.IsNull(systemAgain.When);
                Assert.IsNull(systemAgain.DistributorName);
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Load_ExistingSystemReferences_IgnoresLegacyMap()
        {
            var dir = Path.Combine(Path.GetTempPath(), "twinpack-system-refs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, ".Zeugwerk"));
            File.WriteAllText(Path.Combine(dir, ".Zeugwerk", "config.json"), @"{
                ""fileversion"": 1,
                ""solution"": ""Dummy.sln"",
                ""projects"": [
                    {
                        ""name"": ""MyProject"",
                        ""plcs"": [
                            {
                                ""name"": ""MyPlc"",
                                ""version"": ""1.0.0.0"",
                                ""type"": ""Application"",
                                ""distributor-name"": ""Zeugwerk GmbH"",
                                ""packages"": [
                                    { ""name"": ""ZMotionMc3"", ""version"": ""1.0.0.0"", ""when"": { ""target"": [ ""[TC3.1.4026,)"" ] } }
                                ],
                                ""system-references"": [
                                    { ""name"": ""Tc2_System"", ""version"": ""*"", ""distributor-name"": ""Beckhoff Automation GmbH"" },
                                    { ""name"": ""Tc3_Mc3Base"", ""version"": ""*"", ""distributor-name"": ""Beckhoff Automation GmbH"", ""when"": { ""target"": [ ""[TC3.1.4026,)"" ] } }
                                ],
                                ""references"": {
                                    ""*"": [ ""ShouldBeIgnored=1.0.0.0"" ]
                                }
                            }
                        ]
                    }
                ]
            }");

            try
            {
                var config = ConfigFactory.Load(dir);
                var plc = config.Projects.Single().Plcs.Single();
                Assert.IsNull(plc.References);
                Assert.AreEqual(2, plc.SystemReferences.Count);
                Assert.IsFalse(plc.SystemReferences.Any(x => x.Name == "ShouldBeIgnored"));

                var system = plc.SystemReferences.Single(x => x.Name == "Tc2_System");
                Assert.AreEqual("Beckhoff Automation GmbH", system.DistributorName);
                Assert.IsNull(system.When);

                var mc3 = plc.SystemReferences.Single(x => x.Name == "Tc3_Mc3Base");
                Assert.AreEqual("Beckhoff Automation GmbH", mc3.DistributorName);
                CollectionAssert.AreEqual(new[] { "[TC3.1.4026,)" }, mc3.When.Target.ToArray());

                var package = plc.Packages.Single();
                Assert.AreEqual("ZMotionMc3", package.Name);
                CollectionAssert.AreEqual(new[] { "[TC3.1.4026,)" }, package.When.Target.ToArray());

                ConfigFactory.Save(config);
                var json = File.ReadAllText(config.FilePath);
                Assert.IsTrue(json.Contains("\"system-references\""));
                Assert.IsFalse(json.Contains("\"references\""));
                StringAssert.Contains(json, "Beckhoff Automation GmbH");
                StringAssert.Contains(json, "[TC3.1.4026,)");

                var reloaded = ConfigFactory.Load(dir);
                var again = reloaded.Projects.Single().Plcs.Single();
                Assert.AreEqual(2, again.SystemReferences.Count);
                Assert.AreEqual("[TC3.1.4026,)", again.Packages.Single().When.Target.Single());
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void WhenTarget_ExactLineAndLowerBound()
        {
            var exact = new ConfigWhen { Target = new List<string> { "TC3.1.4026" } };
            Assert.IsTrue(exact.Matches("TC3.1.4026.10"));
            Assert.IsFalse(exact.Matches("TC3.1.4024.12"));
            Assert.IsFalse(exact.Matches("TC3.1.4028"));
            Assert.IsFalse(exact.Matches(null));
            Assert.IsFalse(exact.Matches("TC3.1"));

            var lower = new ConfigWhen { Target = new List<string> { "[TC3.1.4026,)" } };
            Assert.IsTrue(lower.Matches("TC3.1.4026"));
            Assert.IsTrue(lower.Matches("TC3.1.4026.10"));
            Assert.IsTrue(lower.Matches("TC3.1.4028.1"));
            Assert.IsFalse(lower.Matches("TC3.1.4024"));
            Assert.IsFalse(lower.Matches("TC3.1"));
            Assert.IsFalse(lower.Matches(null));

            var closed = new ConfigWhen { Target = new List<string> { "[TC3.1.4026,TC3.1.4028]" } };
            Assert.IsTrue(closed.Matches("TC3.1.4026"));
            Assert.IsTrue(closed.Matches("TC3.1.4028"));
            Assert.IsFalse(closed.Matches("TC3.1.4024"));
            Assert.IsFalse(closed.Matches("TC3.1.4029"));

            var exclusiveEnd = new ConfigWhen { Target = new List<string> { "[TC3.1.4026,TC3.1.4028)" } };
            Assert.IsTrue(exclusiveEnd.Matches("TC3.1.4026"));
            Assert.IsFalse(exclusiveEnd.Matches("TC3.1.4028"));

            var exclusiveStart = new ConfigWhen { Target = new List<string> { "(TC3.1.4026,)" } };
            Assert.IsFalse(exclusiveStart.Matches("TC3.1.4026"));
            Assert.IsTrue(exclusiveStart.Matches("TC3.1.4028"));

            Assert.IsTrue(ConfigWhen.Applies(null, "TC3.1.4024"));
            Assert.IsTrue(new ConfigWhen().Matches("TC3.1.4024"));
            Assert.IsTrue(new ConfigWhen { Target = new List<string>() }.Matches(null));

            var either = new ConfigWhen { Target = new List<string> { "TC3.1.4024", "[TC3.1.4026,)" } };
            Assert.IsTrue(either.Matches("TC3.1.4024.59"));
            Assert.IsTrue(either.Matches("TC3.1.4028"));
            Assert.IsFalse(either.Matches("TC3.1.4022"));

            var universal = new ConfigWhen { Target = new List<string> { "TC3.1" } };
            Assert.IsTrue(universal.Matches("TC3.1"));
            Assert.IsFalse(universal.Matches("TC3.1.4026.10"));
        }

        [TestMethod]
        public void Load_MigratesPlatformPatchesToTarget_SaveOmitsPlatform()
        {
            var dir = Path.Combine(Path.GetTempPath(), "twinpack-platform-patches-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, ".Zeugwerk"));
            File.WriteAllText(Path.Combine(dir, ".Zeugwerk", "config.json"), @"{
                ""fileversion"": 1,
                ""solution"": ""Dummy.sln"",
                ""projects"": [
                    {
                        ""name"": ""MyProject"",
                        ""plcs"": [
                            {
                                ""name"": ""MyPlc"",
                                ""version"": ""1.0.0.0"",
                                ""type"": ""Application"",
                                ""distributor-name"": ""Zeugwerk GmbH"",
                                ""patches"": {
                                    ""platform"": {
                                        ""*"": [ ""Always.include"" ],
                                        ""TC3.1"": [ ""Always.include"" ],
                                        ""TC3.1.4024.59"": [ ""Always.include"", ""StripMc3.include"" ],
                                        ""TC3.1.4026"": [ ""Mc3.include"", ""Shared.include"" ],
                                        ""TC3.1.4028"": [ ""Shared.include"" ]
                                    },
                                    ""argument"": { ""USE_MC3"": [ ""Mc3Defines.arg"" ] },
                                    ""license"": { ""runtime"": [ { ""key"": ""ABC"", ""oem-id"": ""1"", ""comment"": ""runtime"" } ] }
                                }
                            }
                        ]
                    }
                ]
            }");

            try
            {
                var config = ConfigFactory.Load(dir);
                var patches = config.Projects.Single().Plcs.Single().Patches;
                Assert.IsNull(patches.Platform);
                Assert.AreEqual(4, patches.Target.Count);

                var always = patches.Target.Single(x => x.Path == "Always.include");
                Assert.IsNull(always.When);

                var strip = patches.Target.Single(x => x.Path == "StripMc3.include");
                CollectionAssert.AreEqual(new[] { "TC3.1.4024" }, strip.When.Target.ToArray());
                Assert.IsTrue(strip.When.Matches("TC3.1.4024.10"));
                Assert.IsFalse(strip.When.Matches("TC3.1.4026"));

                var mc3 = patches.Target.Single(x => x.Path == "Mc3.include");
                CollectionAssert.AreEqual(new[] { "TC3.1.4026" }, mc3.When.Target.ToArray());

                var shared = patches.Target.Single(x => x.Path == "Shared.include");
                CollectionAssert.AreEquivalent(new[] { "TC3.1.4026", "TC3.1.4028" }, shared.When.Target);

                CollectionAssert.AreEqual(new[] { "Mc3Defines.arg" }, patches.Argument["USE_MC3"].ToArray());
                Assert.AreEqual("ABC", patches.License["runtime"].Single().Key);
                Assert.AreEqual("1", patches.License["runtime"].Single().OemId);

                ConfigFactory.Save(config);
                var json = File.ReadAllText(config.FilePath);
                Assert.IsTrue(json.Contains("\"target\""));
                Assert.IsFalse(json.Contains("\"platform\""));
                StringAssert.Contains(json, "USE_MC3");
                StringAssert.Contains(json, "\"key\": \"ABC\"");

                var again = ConfigFactory.Load(dir).Projects.Single().Plcs.Single().Patches;
                Assert.IsNull(again.Platform);
                Assert.IsNull(again.Target.Single(x => x.Path == "Always.include").When);
                Assert.AreEqual("TC3.1.4024", again.Target.Single(x => x.Path == "StripMc3.include").When.Target.Single());
                CollectionAssert.AreEqual(new[] { "Mc3Defines.arg" }, again.Argument["USE_MC3"].ToArray());
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Load_ExistingTargetPatches_IgnoresPlatformMap()
        {
            var dir = Path.Combine(Path.GetTempPath(), "twinpack-target-patches-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, ".Zeugwerk"));
            File.WriteAllText(Path.Combine(dir, ".Zeugwerk", "config.json"), @"{
                ""fileversion"": 1,
                ""solution"": ""Dummy.sln"",
                ""projects"": [
                    {
                        ""name"": ""MyProject"",
                        ""plcs"": [
                            {
                                ""name"": ""MyPlc"",
                                ""version"": ""1.0.0.0"",
                                ""type"": ""Application"",
                                ""distributor-name"": ""Zeugwerk GmbH"",
                                ""patches"": {
                                    ""target"": [
                                        { ""path"": ""Mc3.include"", ""when"": { ""target"": [ ""[TC3.1.4026,)"" ] } }
                                    ],
                                    ""platform"": { ""*"": [ ""Ignored.include"" ] },
                                    ""argument"": { ""USE_MC3"": [ ""Mc3Defines.arg"" ] },
                                    ""license"": { ""runtime"": [ { ""key"": ""ABC"", ""oem-id"": ""1"", ""comment"": ""runtime"" } ] }
                                }
                            }
                        ]
                    }
                ]
            }");

            try
            {
                var patches = ConfigFactory.Load(dir).Projects.Single().Plcs.Single().Patches;
                Assert.IsNull(patches.Platform);
                var mc3 = patches.Target.Single();
                Assert.AreEqual("Mc3.include", mc3.Path);
                CollectionAssert.AreEqual(new[] { "[TC3.1.4026,)" }, mc3.When.Target.ToArray());
                Assert.IsTrue(mc3.When.Matches("TC3.1.4028"));
                Assert.IsFalse(mc3.When.Matches("TC3.1.4024"));
                Assert.IsFalse(patches.Target.Any(x => x.Path == "Ignored.include"));
                CollectionAssert.AreEqual(new[] { "Mc3Defines.arg" }, patches.Argument["USE_MC3"].ToArray());
                Assert.AreEqual("runtime", patches.License["runtime"].Single().Comment);

                var config = ConfigFactory.Load(dir);
                ConfigFactory.Save(config);
                var json = File.ReadAllText(config.FilePath);
                Assert.IsFalse(json.Contains("\"platform\""));
                Assert.IsFalse(json.Contains("Ignored.include"));
                StringAssert.Contains(json, "[TC3.1.4026,)");
                StringAssert.Contains(json, "USE_MC3");
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }
    }
}
