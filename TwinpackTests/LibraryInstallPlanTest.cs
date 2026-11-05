using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace TwinpackTests
{
    [TestClass]
    public class LibraryInstallPlanTest
    {
        [TestMethod]
        public void FromCache_SelectsLibraryFilesAndSkipsRepositoryPayload()
        {
            var root = NewCache();
            Touch(root, "TC3.1", "Keep_1.0.0.0.library");
            Touch(root, "TC3.1", "Also_2.0.0.0.compiled-library");
            Touch(root, "TC3.1", "Both_3.0.0.0.library");
            Touch(root, "TC3.1", "Both_3.0.0.0.compiled-library");
            Touch(root, "TC3.1", "Keep_1.0.0.0.repository", "payload.library");

            var batch = LibraryInstallPlan.FromCache(root);

            var names = batch.Select(item => item.PackageVersion.LibraryFiles[0]).Select(Path.GetFileName).ToList();
            CollectionAssert.Contains(names, "Keep_1.0.0.0.library");
            CollectionAssert.Contains(names, "Also_2.0.0.0.compiled-library");
            CollectionAssert.Contains(names, "Both_3.0.0.0.compiled-library");
            CollectionAssert.DoesNotContain(names, "Both_3.0.0.0.library");
            CollectionAssert.DoesNotContain(names, "payload.library");
            Assert.AreEqual(3, batch.Count);
        }

        [TestMethod]
        public void FromDownloaded_SelectsOnlyTheFetchedSet()
        {
            var root = NewCache();
            Touch(root, "TC3.1", "ZCore_1.2.0.41.compiled-library");
            Touch(root, "TC3.1", "ZAux_1.0.0.0.library");
            Touch(root, "TC3.1", "Stale_9.9.9.9.library");

            var batch = LibraryInstallPlan.FromDownloaded(new[]
            {
                Package("ZCore", "1.2.0.41", "TC3.1"),
                Package("ZAux", "1.0.0.0", "TC3.1"),
                Package("Missing", "1.0.0.0", "TC3.1")
            }, root);

            var names = batch.Select(item => item.PackageVersion.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "ZCore", "ZAux" }, names);
            Assert.IsFalse(names.Contains("Stale"));
            Assert.IsFalse(names.Contains("Missing"));
        }

        [TestMethod]
        public void FromDownloaded_MultiplePackages_AreOneBatch()
        {
            var root = NewCache();
            Touch(root, "TC3.1", "One_1.0.0.0.library");
            Touch(root, "TC3.1", "Two_1.0.0.0.library");
            Touch(root, "TC3.1", "Three_1.0.0.0.compiled-library");

            var batch = LibraryInstallPlan.FromDownloaded(new[]
            {
                Package("One", "1.0.0.0", "TC3.1"),
                Package("Two", "1.0.0.0", "TC3.1"),
                Package("Three", "1.0.0.0", "TC3.1")
            }, root);

            Assert.AreEqual(3, batch.Count);
            Assert.IsInstanceOfType(batch, typeof(List<PackageItem>));
            CollectionAssert.AreEquivalent(
                new[] { "One", "Two", "Three" },
                batch.Select(item => item.PackageVersion.Name).ToList());
        }

        static string NewCache()
        {
            var root = Path.Combine(Path.GetTempPath(), "twinpack-install-" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            return root;
        }

        static void Touch(string root, string target, string name, string nested = null)
        {
            var directory = nested == null
                ? Path.Combine(root, target)
                : Path.Combine(root, target, name);
            Directory.CreateDirectory(directory);
            var file = nested == null ? Path.Combine(directory, name) : Path.Combine(directory, nested);
            File.WriteAllBytes(file, new byte[] { 0 });
        }

        static PackageItem Package(string name, string version, string target)
        {
            return new PackageItem
            {
                PackageVersion = new PackageVersionGetResponse
                {
                    Name = name,
                    Version = version,
                    Target = target,
                    Branch = "main",
                    Configuration = "Release"
                }
            };
        }
    }
}
