using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Exceptions;
using Twinpack.Models;
using Twinpack.Protocol;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    [TestClass]
    public class PackageMirrorTest
    {
        string _root;
        TimeSpan _targetRequestDelay;

        [TestInitialize]
        public void TestInitialize()
        {
            _targetRequestDelay = PackageMirror.TargetRequestDelay;
            PackageMirror.TargetRequestDelay = TimeSpan.Zero;
            _root = Path.Combine(Path.GetTempPath(), "twinpack-mirror-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            PackageMirror.TargetRequestDelay = _targetRequestDelay;
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [TestMethod]
        public async Task NugetFolderMirrorCopiesNupkgAndSkipsOnSecondRunAsync()
        {
            var sourceDir = Path.Combine(_root, "source");
            var targetDir = Path.Combine(_root, "target");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Pack(sourceDir, "Demo.Library", "1.0.0", new[] { "library", "plc-library" });

            var source = new NugetServer("Package Server Name #1", sourceDir);
            var target = new NugetServer("Package Server Name #2", targetDir);
            await source.LoginAsync();
            await target.LoginAsync();

            var first = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, first.Failed, string.Join("; ", first.Failures));
            Assert.AreEqual(1, first.Copied);
            Assert.AreEqual(0, first.Skipped);
            Assert.IsTrue(Directory.GetFiles(targetDir, "*.nupkg").Length >= 1);

            var second = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, second.Failed, string.Join("; ", second.Failures));
            Assert.AreEqual(0, second.Copied);
            Assert.AreEqual(1, second.Skipped);
        }

        [TestMethod]
        public async Task PackageFilterCopiesOnlyTheNamedLibraryAsync()
        {
            var sourceDir = Path.Combine(_root, "source");
            var targetDir = Path.Combine(_root, "target");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            Pack(sourceDir, "Demo.Library", "1.0.0", new[] { "library" });
            Pack(sourceDir, "Other.Library", "1.0.0", new[] { "library" });

            var source = new NugetServer("Package Server Name #1", sourceDir);
            var target = new NugetServer("Package Server Name #2", targetDir);
            await source.LoginAsync();
            await target.LoginAsync();

            var result = await PackageMirror.RunAsync(source, target, new[] { "Demo.Library" });
            Assert.AreEqual(0, result.Failed, string.Join("; ", result.Failures));
            Assert.AreEqual(1, result.Copied);
            Assert.AreEqual(1, Directory.GetFiles(targetDir, "Demo.Library.*.nupkg").Length);
            Assert.AreEqual(0, Directory.GetFiles(targetDir, "Other.Library.*.nupkg").Length);

            var missing = await PackageMirror.RunAsync(source, target, new[] { "Missing.Library" });
            Assert.AreEqual(1, missing.Failed);
            Assert.AreEqual(0, missing.Copied);
        }

        [TestMethod]
        public async Task BeckhoffSourceCopiesOntoANugetFolderAsync()
        {
            var sourceDir = Path.Combine(_root, "beckhoff");
            var targetDir = Path.Combine(_root, "nuget");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            // A local folder feed matches the search text as a substring. Beckhoff's query is
            // "tags:library", which a remote feed treats as a tag filter.
            Pack(sourceDir, "TwinCAT.XAE.PLC.Lib.Tc2_Standard", "3.4.5", new[] { "library", "tags:library" });

            var source = new BeckhoffServer("Beckhoff", sourceDir);
            var target = new NugetServer("Private NuGet", targetDir);
            await source.LoginAsync();
            await target.LoginAsync();

            var result = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, result.Failed, string.Join("; ", result.Failures));
            Assert.AreEqual(1, result.Copied);
            Assert.AreEqual(0, result.Skipped);
            var published = Directory.GetFiles(targetDir, "Tc2_Standard.*.nupkg");
            Assert.AreEqual(1, published.Length, "a Beckhoff package is published under the library name");
            using (var archive = System.IO.Compression.ZipFile.OpenRead(published[0]))
            {
                Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.EndsWith(".library", StringComparison.OrdinalIgnoreCase)));
                Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)));
                Assert.IsFalse(archive.Entries.Any(entry => entry.FullName.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase)));
                var nuspec = archive.Entries.First(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
                using (var reader = new StreamReader(nuspec.Open()))
                {
                    var text = reader.ReadToEnd();
                    Assert.IsTrue(text.IndexOf("<title>Tc2_Standard</title>", StringComparison.Ordinal) >= 0, text);
                }
            }
        }

        [TestMethod]
        public async Task TwinpackMirrorCopiesOneVersionAndSkipsOnSecondRunAsync()
        {
            var source = new FakeTwinpackServer("source");
            source.Catalog.Add(new CatalogItemGetResponse { Name = "LibA", DistributorName = "Zeugwerk" });
            source.Versions.Add(new PackageVersionGetResponse
            {
                Name = "LibA",
                Version = "1.2.3.4",
                DistributorName = "Zeugwerk",
                Branch = "main",
                Target = "TC3.1",
                Configuration = "Release",
                Description = "copied",
                Authors = "Zeugwerk",
                Dependencies = new List<PackageVersionGetResponse>
                {
                    new PackageVersionGetResponse
                    {
                        Name = "Dep",
                        Version = "1.0.0",
                        Branch = "main",
                        Target = "TC3.1",
                        Configuration = "Release",
                        DistributorName = "Beckhoff"
                    }
                }
            });
            var target = new FakeTwinpackServer("target");

            var first = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, first.Failed, string.Join("; ", first.Failures));
            Assert.AreEqual(1, first.Copied);
            Assert.AreEqual(0, first.Skipped);
            Assert.AreEqual(1, target.Posted.Count);
            Assert.AreEqual("copied", target.Posted[0].Description);
            Assert.AreEqual("Zeugwerk", target.Posted[0].DistributorName);
            Assert.AreEqual(Convert.ToBase64String(Encoding.UTF8.GetBytes("library-bytes")), target.Posted[0].Binary);
            Assert.AreEqual("Dep", target.Posted[0].Dependencies.Single().Name);
            Assert.AreEqual("1.0.0", target.Posted[0].Dependencies.Single().Version);

            var second = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, second.Failed, string.Join("; ", second.Failures));
            Assert.AreEqual(0, second.Copied);
            Assert.AreEqual(1, second.Skipped);
            Assert.AreEqual(1, target.Posted.Count);
        }

        [TestMethod]
        public async Task NugetLibraryIsPostedToTwinpackAsync()
        {
            var sourceDir = Path.Combine(_root, "nuget");
            Directory.CreateDirectory(sourceDir);
            Pack(sourceDir, "Demo.Library", "1.0.0", new[] { "library" });

            var source = new NugetServer("nuget", sourceDir);
            await source.LoginAsync();
            var target = new FakeTwinpackServer("twinpack");

            var result = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, result.Failed, string.Join("; ", result.Failures));
            Assert.AreEqual(1, result.Copied);
            Assert.AreEqual(1, target.Posted.Count);
            Assert.AreEqual("Demo.Library", target.Posted[0].Name);
            Assert.AreEqual(Convert.ToBase64String(Encoding.UTF8.GetBytes("library")), target.Posted[0].Binary);
        }

        [TestMethod]
        public async Task TwinpackLibraryIsPackedOntoANugetFolderAsync()
        {
            var targetDir = Path.Combine(_root, "nuget");
            Directory.CreateDirectory(targetDir);
            var target = new NugetServer("nuget", targetDir);
            await target.LoginAsync();

            var source = new FakeTwinpackServer("twinpack");
            source.Catalog.Add(new CatalogItemGetResponse { Name = "LibA", DistributorName = "Zeugwerk" });
            source.Versions.Add(new PackageVersionGetResponse
            {
                Name = "LibA",
                Version = "1.2.3.4",
                DistributorName = "Zeugwerk",
                Branch = "main",
                Target = "TC3.1",
                Configuration = "Release",
                Description = "copied"
            });

            var result = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, result.Failed, string.Join("; ", result.Failures));
            Assert.AreEqual(1, result.Copied);
            Assert.IsTrue(Directory.GetFiles(targetDir, "LibA.*.nupkg").Any());
        }

        [TestMethod]
        public async Task RepositoryOnlyPackageIsNotPushedToTwinpackAsync()
        {
            var sourceDir = Path.Combine(_root, "tmx");
            Directory.CreateDirectory(sourceDir);
            var tmx = Path.Combine(sourceDir, "Widget.tmx");
            File.WriteAllText(tmx, "repository");
            NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = "Only.Tmx",
                Version = "1.0.0",
                Authors = "Test",
                Description = "repository file",
                Tags = new[] { "library" },
                Files = new[] { new NugetPackFile { Source = tmx, Target = "TC3.1" } },
                OutputDirectory = sourceDir
            });

            var source = new NugetServer("nuget", sourceDir);
            await source.LoginAsync();
            var target = new FakeTwinpackServer("twinpack");

            var result = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, result.Failed, string.Join("; ", result.Failures));
            Assert.AreEqual(0, result.Copied);
            Assert.AreEqual(1, result.Skipped);
            Assert.AreEqual(0, target.Posted.Count);
        }

        [TestMethod]
        public async Task RepositoryPackageIsCopiedOntoANugetFolderAsync()
        {
            var sourceDir = Path.Combine(_root, "tmx-source");
            var targetDir = Path.Combine(_root, "tmx-target");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            var tmx = Path.Combine(sourceDir, "Widget.tmx");
            File.WriteAllText(tmx, "repository");
            NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = "TwinCAT.XAE.TMX.Widget",
                Version = "1.0.0",
                Authors = "Test",
                Description = "repository file",
                Tags = new[] { "tmx" },
                Files = new[]
                {
                    new NugetPackFile
                    {
                        Source = tmx,
                        Target = "repository/Beckhoff Automation GmbH/Widget/1.0.0/TwinCAT OS (x64)"
                    }
                },
                OutputDirectory = sourceDir
            });

            var source = new NugetServer("nuget", sourceDir);
            var target = new NugetServer("local", targetDir);
            await source.LoginAsync();
            await target.LoginAsync();

            var result = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, result.Failed, string.Join("; ", result.Failures));
            Assert.AreEqual(1, result.Copied);
            var published = Directory.GetFiles(targetDir, "TwinCAT.XAE.TMX.Widget.*.nupkg");
            Assert.AreEqual(1, published.Length);
            using (var archive = System.IO.Compression.ZipFile.OpenRead(published[0]))
            {
                var entry = archive.Entries.FirstOrDefault(item => item.FullName.EndsWith("Widget.tmx", StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(entry, "the tmx file is in the mirrored package");
                StringAssert.Contains(entry.FullName.Replace('\\', '/'), "repository/Beckhoff Automation GmbH/Widget/1.0.0/");
            }

            var second = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, second.Failed, string.Join("; ", second.Failures));
            Assert.AreEqual(0, second.Copied);
            Assert.AreEqual(1, second.Skipped);
        }

        [TestMethod]
        public async Task LibraryMirrorKeepsRepositoryFilesOnANugetTargetAsync()
        {
            var sourceDir = Path.Combine(_root, "lib-source");
            var targetDir = Path.Combine(_root, "lib-target");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            var library = Path.Combine(sourceDir, "Demo.Library.library");
            var tmx = Path.Combine(sourceDir, "Demo.tmx");
            File.WriteAllText(library, "library");
            File.WriteAllText(tmx, "repository");
            NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = "Demo.Library",
                Version = "1.0.0",
                Authors = "Test",
                Description = "library plus tmx",
                Tags = new[] { "library" },
                Files = new[]
                {
                    new NugetPackFile { Source = library, Target = "TC3.1" },
                    new NugetPackFile { Source = tmx, Target = "repository/Beckhoff Automation GmbH/Demo/1.0.0" }
                },
                OutputDirectory = sourceDir
            });

            var source = new NugetServer("nuget", sourceDir);
            var target = new NugetServer("local", targetDir);
            await source.LoginAsync();
            await target.LoginAsync();

            var result = await PackageMirror.RunAsync(source, target);
            Assert.AreEqual(0, result.Failed, string.Join("; ", result.Failures));
            Assert.AreEqual(1, result.Copied);
            var published = Directory.GetFiles(targetDir, "Demo.Library.*.nupkg");
            Assert.AreEqual(1, published.Length);
            using (var archive = System.IO.Compression.ZipFile.OpenRead(published[0]))
            {
                Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.EndsWith(".library", StringComparison.OrdinalIgnoreCase)));
                Assert.IsTrue(archive.Entries.Any(entry => entry.FullName.EndsWith("Demo.tmx", StringComparison.OrdinalIgnoreCase)));
            }
        }

        [TestMethod]
        public async Task SameServerIsRejectedAsync()
        {
            var feed = new NugetServer("Feed", Path.Combine(_root, "feed"));
            var sameName = new NugetServer("feed", Path.Combine(_root, "other"));
            await Assert.ThrowsExceptionAsync<ProtocolException>(() => PackageMirror.RunAsync(feed, sameName));

            var left = new NugetServer("Left", Path.Combine(_root, "shared"));
            var right = new NugetServer("Right", Path.Combine(_root, "shared") + "\\");
            await Assert.ThrowsExceptionAsync<ProtocolException>(() => PackageMirror.RunAsync(left, right));
        }

        [TestMethod]
        public void ResolveMatchesServerNameIgnoringCase()
        {
            var servers = new List<IPackageServer>
            {
                new NugetServer("Package Server Name #1", "https://one.example"),
                new NugetServer("Package Server Name #2", "https://two.example")
            };

            var resolved = PackageMirror.Resolve("package server name #1", servers);
            Assert.AreEqual("Package Server Name #1", resolved.Name);

            var missing = Assert.ThrowsException<ProtocolException>(() => PackageMirror.Resolve("missing", servers));
            StringAssert.Contains(missing.Message, "Package Server Name #1");
        }

        static void Pack(string directory, string id, string version, IEnumerable<string> tags)
        {
            var library = Path.Combine(directory, id + ".library");
            File.WriteAllText(library, "library");
            NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = id,
                Version = version,
                Authors = "Test",
                Description = "mirror test",
                Tags = tags,
                Files = new[] { new NugetPackFile { Source = library, Target = "TC3.1" } },
                OutputDirectory = directory
            });
        }

        sealed class FakeTwinpackServer : TwinpackServer
        {
            public List<CatalogItemGetResponse> Catalog { get; } = new List<CatalogItemGetResponse>();
            public List<PackageVersionGetResponse> Versions { get; } = new List<PackageVersionGetResponse>();
            public List<PackageVersionPostRequest> Posted { get; } = new List<PackageVersionPostRequest>();

            public FakeTwinpackServer(string name)
                : base(name, "https://twinpack.example/" + name)
            {
            }

            public override Task<Tuple<IEnumerable<CatalogItemGetResponse>, bool>> GetCatalogAsync(string search, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
            {
                if (page != 1)
                    return Task.FromResult(new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(new List<CatalogItemGetResponse>(), false));

                return Task.FromResult(new Tuple<IEnumerable<CatalogItemGetResponse>, bool>(Catalog.ToList(), false));
            }

            public override Task<Tuple<IEnumerable<PackageVersionGetResponse>, bool>> GetPackageVersionsAsync(PlcLibrary library, string branch = null, string configuration = null, string target = null, int page = 1, int perPage = 5, CancellationToken cancellationToken = default)
            {
                if (page != 1)
                    return Task.FromResult(new Tuple<IEnumerable<PackageVersionGetResponse>, bool>(new List<PackageVersionGetResponse>(), false));

                var matches = Versions
                    .Where(x => string.Equals(x.Name, library?.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return Task.FromResult(new Tuple<IEnumerable<PackageVersionGetResponse>, bool>(matches, false));
            }

            public override Task<PackageVersionGetResponse> GetPackageVersionAsync(PlcLibrary library, string branch, string configuration, string target, CancellationToken cancellationToken = default)
            {
                var match = Versions.FirstOrDefault(x =>
                    string.Equals(x.Name, library?.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Version, library?.Version, StringComparison.OrdinalIgnoreCase)
                    && (branch == null || string.Equals(x.Branch, branch, StringComparison.OrdinalIgnoreCase))
                    && (configuration == null || string.Equals(x.Configuration, configuration, StringComparison.OrdinalIgnoreCase))
                    && (target == null || string.Equals(x.Target, target, StringComparison.OrdinalIgnoreCase)));
                return Task.FromResult(match ?? new PackageVersionGetResponse());
            }

            public override Task DownloadPackageVersionAsync(PackageVersionGetResponse packageVersion, ChecksumMode checksumMode, string cachePath = null, CancellationToken cancellationToken = default)
            {
                var extension = packageVersion.Compiled == 1 ? "compiled-library" : "library";
                var directory = string.IsNullOrEmpty(packageVersion.Target) ? cachePath : Path.Combine(cachePath, packageVersion.Target);
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(
                    Path.Combine(directory, $"{packageVersion.Name}_{packageVersion.Version}.{extension}"),
                    Encoding.UTF8.GetBytes("library-bytes"));
                return Task.CompletedTask;
            }

            public override Task<PackageVersionGetResponse> PostPackageVersionAsync(PackageVersionPostRequest packageVersion, CancellationToken cancellationToken = default)
            {
                Posted.Add(packageVersion);
                Versions.Add(new PackageVersionGetResponse
                {
                    Name = packageVersion.Name,
                    Version = packageVersion.Version,
                    Branch = packageVersion.Branch,
                    Target = packageVersion.Target,
                    Configuration = packageVersion.Configuration,
                    DistributorName = packageVersion.DistributorName,
                    Description = packageVersion.Description,
                    PackageVersionId = Posted.Count
                });
                return Task.FromResult(new PackageVersionGetResponse
                {
                    PackageVersionId = Posted.Count,
                    Name = packageVersion.Name,
                    Version = packageVersion.Version
                });
            }
        }
    }
}
