using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

                Assert.AreEqual("library tp-library-version=1.0.2.4", nuspec.GetTags());

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

                // ... which is why the package says which library version it really is
                CollectionAssert.Contains(tags, "tp-library-version=2.0.0.0");
            }
        }

        [DataTestMethod]
        [DataRow("1.2.3.0", "tp-library-version=1.2.3.0")]
        [DataRow("1.2.3.0-feat-ci", "tp-library-version=1.2.3.0")]
        [DataRow("1.2.3.4-release-1-x", "tp-library-version=1.2.3.4")]
        [DataRow("1.2.3", "tp-library-version=1.2.3")]
        [DataRow("1.2", "tp-library-version=1.2")]
        [DataRow("1.2.3-feat-ci.1", "tp-library-version=1.2.3.1")]
        [DataRow("1.02.3.0", "tp-library-version=1.2.3.0")]
        [DataRow("*", null)]
        [DataRow("", null)]
        [DataRow(null, null)]
        [DataRow("feat-ci", null)]
        public void LibraryVersionTagIsTheVersionSetVersionWritesIntoThePlcproj(string configVersion, string expected)
        {
            Assert.AreEqual(expected, NugetPackService.LibraryVersionTag(configVersion));
            if (expected != null)
                Assert.AreEqual(expected, $"{NugetPackService.LibraryVersionTagKey}={AutomationInterface.TwinCATProjectVersion(configVersion)}");
        }

        [DataTestMethod]
        [DataRow("library tp-compiled-library tp-library-version=1.2.3.0", "1.2.3.0")]
        [DataRow("library,tp-library-version=1.2.3.0,tp-compiled-library", "1.2.3.0")]
        [DataRow("LIBRARY TP-LIBRARY-VERSION=1.2.3", "1.2.3")]
        [DataRow("library tp-library-version=", null)]
        [DataRow("library tp-library-version=latest", null)]
        [DataRow("library tp-compiled-library", null)]
        [DataRow("library library-version=1.2.3.0", null)]
        [DataRow(null, null)]
        public void LibraryVersionFromTagsReadsOnlyAWellFormedTag(string tags, string expected)
        {
            Assert.AreEqual(expected, NugetPackService.LibraryVersionFromTags(tags));
        }

        [TestMethod]
        public void TwinpackTagValuesSkipsFlagsAndForeignTags()
        {
            var values = NugetPackService.TwinpackTagValues("library tp-compiled-library tp-library-version=1.2.3.0 other=1 tp-=x");

            Assert.AreEqual(1, values.Count);
            Assert.AreEqual("1.2.3.0", values["tp-library-version"]);
        }

        /// <summary>
        /// The case the tag exists for: a library versioned x.y.z.0 is published as x.y.z, and only the
        /// tag tells Twinpack that the library TwinCAT registers is x.y.z.0. The prerelease label from
        /// config.json is part of the package version and comes back with it.
        /// </summary>
        [DataTestMethod]
        [DataRow("1.2.3.0", "1.2.3.0")]
        [DataRow("1.2.3.0-feat-ci", "1.2.3.0-feat-ci")]
        [DataRow("1.2.3.4", "1.2.3.4")]
        [DataRow("1.2.3", "1.2.3")]
        [DataRow("1.2", "1.2")]
        public async Task NugetServerReadsTheLibraryVersionBackAsync(string configVersion, string expected)
        {
            var feed = Path.Combine(_output, "feed");
            var plc = new ConfigPlcProject { Name = "PlcLibrary1", Version = configVersion, DistributorName = "MyCompany" };
            NugetPackService.PackLibrary(plc, _library, "TC3.1", false, feed);

            var server = new NugetServer("Local NuGet", feed);
            await server.LoginAsync();

            var package = await server.GetPackageVersionAsync(new Twinpack.Models.PlcLibrary { Name = "PlcLibrary1" }, null, null, null);
            Assert.AreEqual(expected, package.Version);
            Assert.IsFalse(package.VersionAmbiguous);

            var listed = (await server.GetPackageVersionsAsync(new Twinpack.Models.PlcLibrary { Name = "PlcLibrary1" })).Item1.Single();
            Assert.AreEqual(expected, listed.Version);

            // looking it up by the version Twinpack now reports finds the same package
            var byVersion = await server.GetPackageVersionAsync(new Twinpack.Models.PlcLibrary { Name = "PlcLibrary1", Version = expected }, null, null, null);
            Assert.AreEqual(expected, byVersion.Version);
        }

        /// <summary>
        /// A package published before the tag existed, or by another tool, still reads as the NuGet
        /// version, and is marked as not saying which library version it is.
        /// </summary>
        [TestMethod]
        public async Task NugetServerReadsAnUntaggedPackageAsBeforeAsync()
        {
            var feed = Path.Combine(_output, "feed");
            NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = "PlcLibrary1",
                Version = "1.2.3.0",
                Authors = "MyCompany",
                Description = "untagged",
                Tags = new[] { NugetPackService.LibraryTag },
                Files = new[] { new NugetPackFile { Source = _library, Target = "TC3.1" } },
                OutputDirectory = feed
            });

            var server = new NugetServer("Local NuGet", feed);
            await server.LoginAsync();

            var package = await server.GetPackageVersionAsync(new Twinpack.Models.PlcLibrary { Name = "PlcLibrary1" }, null, null, null);
            Assert.AreEqual("1.2.3", package.Version);
            Assert.IsTrue(package.VersionAmbiguous);
        }

        [TestMethod]
        public async Task NugetServerIgnoresATagForAnotherVersionAsync()
        {
            var feed = Path.Combine(_output, "feed");
            NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = "PlcLibrary1",
                Version = "1.2.3",
                Authors = "MyCompany",
                Description = "wrong tag",
                Tags = new[] { NugetPackService.LibraryTag, "tp-library-version=1.2.4.0" },
                Files = new[] { new NugetPackFile { Source = _library, Target = "TC3.1" } },
                OutputDirectory = feed
            });

            var server = new NugetServer("Local NuGet", feed);
            await server.LoginAsync();

            var package = await server.GetPackageVersionAsync(new Twinpack.Models.PlcLibrary { Name = "PlcLibrary1" }, null, null, null);
            Assert.AreEqual("1.2.3", package.Version);
            Assert.IsTrue(package.VersionAmbiguous);
        }

        /// <summary>
        /// A dependency range only holds the NuGet version ([1.4.2, ) for ZCore 1.4.2.0). The package
        /// the range resolves to carries the tag, so the dependency is the library version again.
        /// </summary>
        [TestMethod]
        public async Task DependencyResolvesToTheTaggedLibraryVersionAsync()
        {
            var feed = Path.Combine(_output, "feed");
            NugetPackService.PackLibrary(
                new ConfigPlcProject { Name = "ZCore", Version = "1.4.2.0", DistributorName = "MyCompany" },
                _library, "TC3.1", false, feed);
            NugetPackService.PackLibrary(
                new ConfigPlcProject
                {
                    Name = "PlcLibrary1",
                    Version = "1.0.0.1",
                    DistributorName = "MyCompany",
                    Packages = new List<ConfigPlcPackage> { new ConfigPlcPackage { Name = "ZCore", Version = "1.4.2.0" } }
                },
                _library, "TC3.1", false, feed);

            var server = new NugetServer("Local NuGet", feed);
            await server.LoginAsync();

            var package = await server.GetPackageVersionAsync(new Twinpack.Models.PlcLibrary { Name = "PlcLibrary1" }, null, null, null);
            var dependency = package.Dependencies.Single();
            Assert.AreEqual("ZCore", dependency.Name);
            Assert.AreEqual("1.4.2.0", dependency.Version);
            Assert.IsFalse(dependency.VersionAmbiguous);
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

        [TestMethod]
        public async Task LocalFeedCatalogReadsTheEmbeddedIcon()
        {
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            var icon = Path.Combine(_output, "icon.png");
            File.WriteAllBytes(icon, png);

            var feed = Path.Combine(_output, "feed");
            Directory.CreateDirectory(feed);

            var packed = NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = "Twinpack.IconSample",
                Version = "1.0.0",
                Title = "Twinpack Icon Sample",
                Authors = "Zeugwerk",
                Description = "Sample package with an embedded icon",
                Tags = new[] { "library" },
                IconFile = icon,
                OutputDirectory = feed
            });

            using (var reader = new PackageArchiveReader(File.OpenRead(packed)))
            {
                Assert.AreEqual("icon.png", reader.NuspecReader.GetIcon());
                var entry = reader.GetEntry(reader.NuspecReader.GetIcon());
                Assert.IsNotNull(entry, "the icon file has to sit at the path the nuspec names");
            }

            var server = new NugetServer("Local NuGet", feed);
            await server.LoginAsync();

            var catalog = await server.GetCatalogAsync(null);

            var package = catalog.Item1.Single();
            CollectionAssert.AreEqual(png, package.IconBytes);
        }
    }
}
