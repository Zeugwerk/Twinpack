using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class NugetPackBuilderTest
    {
        const string _library = @"assets\LibCat1_All_selected.library";

        string _output;

        [TestInitialize]
        public void TestInitialize()
        {
            _output = Path.Combine(Path.GetTempPath(), "twinpack-pack-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_output);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (Directory.Exists(_output))
                Directory.Delete(_output, true);
        }

        [DataTestMethod]
        [DataRow("PlcLibrary1", "PlcLibrary1")]
        [DataRow("My Library With Spaces", "My.Library.With.Spaces")]
        [DataRow("Lib/With\\Slashes", "Lib.With.Slashes")]
        [DataRow("..Leading.And.Trailing..", "Leading.And.Trailing")]
        [DataRow("Keeps_Underscores-And.Dots", "Keeps_Underscores-And.Dots")]
        [DataRow("", "package")]
        [DataRow(null, "package")]
        public void PackageIdReplacesEverythingNuGetDisallows(string name, string expected)
        {
            Assert.AreEqual(expected, NugetPackBuilder.PackageId(name));
        }

        [TestMethod]
        public void PackLibraryProducesAPackageNugetServerCanConsume()
        {
            var plc = new ConfigPlcProject
            {
                Name = "PlcLibrary1",
                Version = "1.0.2.4",
                DistributorName = "MyCompany",
                Packages = new List<ConfigPlcPackage>
                {
                    new ConfigPlcPackage { Name = "ZCore", Version = "1.4.2.0" },
                    new ConfigPlcPackage { Name = "Any Version Lib", Version = null }
                }
            };

            var path = NugetPackService.PackLibrary(plc, _library, "TC3.1", compiled: false, outputDirectory: _output);

            // NuGet understands the four part version as it is
            Assert.AreEqual("PlcLibrary1.1.0.2.4.nupkg", Path.GetFileName(path));

            using (var reader = new PackageArchiveReader(File.OpenRead(path)))
            {
                var nuspec = reader.NuspecReader;

                Assert.AreEqual("PlcLibrary1", nuspec.GetId());
                Assert.AreEqual("1.0.2.4", nuspec.GetVersion().ToNormalizedString());

                // the version the package is published under has to resolve back to what the
                // project asked for, otherwise Twinpack cannot match it
                Assert.AreEqual("1.0.2.4", AutomationInterface.FourPartVersion(nuspec.GetVersion().ToNormalizedString()));

                // authors is what NugetServer reads back as the distributor name, so it carries the
                // PLC's Company and not the library's Author property ("MyAuthor")
                Assert.AreEqual("MyCompany", nuspec.GetAuthors());

                // title and description come from the library binary's own TwinCAT properties
                Assert.AreEqual("MyTitle", nuspec.GetTitle());
                Assert.AreEqual("MyDescription", nuspec.GetDescription());

                Assert.AreEqual("library", nuspec.GetTags());

                var files = reader.GetFiles().Where(x => x.EndsWith(".library")).ToList();
                Assert.AreEqual(1, files.Count, "NugetServer requires exactly one library in the package");
                Assert.AreEqual("TC3.1/LibCat1_All_selected.library", files.Single());

                var dependencies = nuspec.GetDependencyGroups().SelectMany(x => x.Packages).ToList();
                Assert.AreEqual(2, dependencies.Count);
                Assert.AreEqual("[1.4.2, )", dependencies.Single(x => x.Id == "ZCore").VersionRange.ToNormalizedString());
                Assert.AreEqual("Any.Version.Lib", dependencies.Single(x => x.Id != "ZCore").Id);
            }
        }

        [TestMethod]
        public void PackLibraryTagsCompiledLibrariesSoTheReaderRecognizesThem()
        {
            var plc = new ConfigPlcProject { Name = "PlcLibrary1", Version = "2.0.0.0", DistributorName = "MyCompany" };

            var path = NugetPackService.PackLibrary(plc, _library, "TC3.1", compiled: true, outputDirectory: _output);

            // a revision of 0 is the one part NuGet normalizes away, so this is a stable 2.0.0
            Assert.AreEqual("PlcLibrary1.2.0.0.nupkg", Path.GetFileName(path));

            using (var reader = new PackageArchiveReader(File.OpenRead(path)))
            {
                // NuGet writes the version into the nuspec itself and normalizes the revision of 0 away,
                // so the package is a stable 2.0.0 and nothing downstream can tell it apart from a
                // library whose version was three parts to begin with
                Assert.AreEqual("2.0.0", reader.NuspecReader.GetVersion().OriginalVersion);

                var tags = reader.NuspecReader.GetTags().Split(' ');
                CollectionAssert.Contains(tags, NugetPackService.LibraryTag);
                CollectionAssert.Contains(tags, NugetPackService.CompiledLibraryTag);
            }
        }

        [TestMethod]
        public void PackLibraryFailsWhenTheBuildProducedNoArtifact()
        {
            var plc = new ConfigPlcProject { Name = "Missing", Version = "1.0.0.0" };

            Assert.ThrowsException<Twinpack.Exceptions.LibraryNotFoundException>(
                () => NugetPackService.PackLibrary(plc, @"assets\does-not-exist.library", "TC3.1", false, _output));
        }

        [TestMethod]
        public void PackLibraryKeepsTheRealTitleWhenTheIdHasToBeSanitized()
        {
            var plc = new ConfigPlcProject { Name = "My Library With Spaces", Version = "1.0.0.1", DistributorName = "MyCompany" };

            var path = NugetPackService.PackLibrary(plc, _library, "TC3.1", false, _output,
                new PlcPublishMetadata { DisplayName = "My Library With Spaces", Description = "d", Authors = "a" });

            Assert.AreEqual("My.Library.With.Spaces.1.0.0.1.nupkg", Path.GetFileName(path));

            using (var reader = new PackageArchiveReader(File.OpenRead(path)))
            {
                Assert.AreEqual("My.Library.With.Spaces", reader.NuspecReader.GetId());
                Assert.AreEqual("My Library With Spaces", reader.NuspecReader.GetTitle());
            }
        }

        [TestMethod]
        public void PackLibrarySkipsALicenseNuGetWouldReject()
        {
            var plc = new ConfigPlcProject { Name = "PlcLibrary1", Version = "1.0.0.0", DistributorName = "MyCompany" };

            // ZF1000 is a Zeugwerk license identifier, not SPDX. Packing has to keep working.
            var path = NugetPackService.PackLibrary(plc, _library, "TC3.1", false, _output,
                new PlcPublishMetadata { License = "ZF1000" });

            using (var reader = new PackageArchiveReader(File.OpenRead(path)))
            {
                Assert.IsNull(reader.NuspecReader.GetLicenseMetadata());
            }
        }

        [TestMethod]
        public void PackLibraryKeepsASpdxLicense()
        {
            var plc = new ConfigPlcProject { Name = "PlcLibrary1", Version = "1.0.0.0", DistributorName = "MyCompany" };

            var path = NugetPackService.PackLibrary(plc, _library, "TC3.1", false, _output,
                new PlcPublishMetadata { License = "MIT" });

            using (var reader = new PackageArchiveReader(File.OpenRead(path)))
            {
                Assert.AreEqual("MIT", reader.NuspecReader.GetLicenseMetadata().License);
            }
        }

        [TestMethod]
        public void PackApplicationCarriesTheBootProjectAndTheTmc()
        {
            var boot = Path.Combine(_output, "src", "_Boot");
            Directory.CreateDirectory(Path.Combine(boot, "TwinCAT RT (x64)", "Plc"));
            File.WriteAllText(Path.Combine(boot, "CurrentConfig.xml"), "<config/>");
            File.WriteAllText(Path.Combine(boot, "TwinCAT RT (x64)", "Plc", "Port_851.app"), "app");
            var tmc = Path.Combine(_output, "src", "MyApp.tmc");
            File.WriteAllText(tmc, "<tmc/>");

            var plc = new ConfigPlcProject { Name = "MyApp", Version = "1.4.2.1", DistributorName = "MyCompany" };

            var path = NugetPackService.PackApplication(plc, boot, tmc, _output);

            Assert.AreEqual("MyApp.1.4.2.1.nupkg", Path.GetFileName(path));

            using (var reader = new PackageArchiveReader(File.OpenRead(path)))
            {
                var files = reader.GetFiles().Where(x => !x.StartsWith("_rels") && !x.StartsWith("package/") && !x.Contains(".nuspec") && x != "[Content_Types].xml").ToList();

                CollectionAssert.Contains(files, "_Boot/CurrentConfig.xml");
                CollectionAssert.Contains(files, "_Boot/TwinCAT RT (x64)/Plc/Port_851.app");
                CollectionAssert.Contains(files, "MyApp.tmc");
                Assert.AreEqual("application", reader.NuspecReader.GetTags());
            }
        }

        [TestMethod]
        public void ResolveTargetKeepsWhatTheCallerAskedFor()
        {
            Directory.CreateDirectory(Path.Combine(_output, ".Zeugwerk", "libraries", "TC3.1.4024.56"));

            Assert.AreEqual("TC3.1", LibraryTargets.Resolve("TC3.1", new[] { LibraryTargets.DirectoryOf(_output) }));
        }

        /// <summary>
        /// The case that used to silently read from a folder that does not exist: a build with no
        /// --tcversion resolves the target through the remote manager, so packing has to read the
        /// target back out of the build output rather than assume TC3.1.
        /// </summary>
        [TestMethod]
        public void ResolveTargetTakesTheOneTheBuildProduced()
        {
            Directory.CreateDirectory(Path.Combine(_output, ".Zeugwerk", "libraries", "TC3.1.4024.56"));

            Assert.AreEqual("TC3.1.4024.56", LibraryTargets.Resolve(null, new[] { LibraryTargets.DirectoryOf(_output) }));
        }

        [TestMethod]
        public void ResolveTargetFindsTheTargetInAModuleDirectory()
        {
            var module = Path.Combine(_output, "modules", "D_Core", "src");
            Directory.CreateDirectory(Path.Combine(module, ".Zeugwerk", "libraries", "TC3.1.4026.14"));

            var directories = new[] { LibraryTargets.DirectoryOf(_output), LibraryTargets.DirectoryOf(module) };

            Assert.AreEqual("TC3.1.4026.14", LibraryTargets.Resolve(null, directories));
        }

        [TestMethod]
        public void ResolveTargetRefusesToGuessBetweenSeveralTargets()
        {
            Directory.CreateDirectory(Path.Combine(_output, ".Zeugwerk", "libraries", "TC3.1"));
            Directory.CreateDirectory(Path.Combine(_output, ".Zeugwerk", "libraries", "TC3.1.4024.56"));

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => LibraryTargets.Resolve(null, new[] { LibraryTargets.DirectoryOf(_output) }));

            StringAssert.Contains(ex.Message, "TC3.1");
            StringAssert.Contains(ex.Message, "TC3.1.4024.56");
        }

        [TestMethod]
        public void ResolveTargetFallsBackWhenNothingWasBuilt()
        {
            Assert.AreEqual(LibraryTargets.Default, LibraryTargets.Resolve(null, new[] { LibraryTargets.DirectoryOf(_output) }));
        }
    }
}
