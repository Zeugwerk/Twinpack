using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using Twinpack.Configuration;
using Twinpack.Core;

namespace TwinpackTests
{
    [TestClass]
    public class ConfigPlcFactoryFromPathTest
    {
        const string SourceAsset = @"assets\LibCat1_All_selected.library";

        [TestMethod]
        public void PlcProjectsFromPath_CompiledTrue_SelectsOnlyCompiledLibraries()
        {
            var root = NewTempDir();
            try
            {
                File.Copy(SourceAsset, Path.Combine(root, "Source.library"));
                File.Copy(SourceAsset, Path.Combine(root, "Compiled.compiled-library"));
                File.WriteAllText(Path.Combine(root, "readme.txt"), "ignore");

                var plcs = ConfigPlcProjectFactory.PlcProjectsFromPath(root, new PackageServerCollection(), compiled: true).ToList();

                Assert.AreEqual(1, plcs.Count);
                Assert.IsTrue(plcs[0].Plc.FilePath.EndsWith(".compiled-library", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void PlcProjectsFromPath_CompiledFalse_SelectsOnlySourceLibraries()
        {
            var root = NewTempDir();
            try
            {
                File.Copy(SourceAsset, Path.Combine(root, "Source.library"));
                File.Copy(SourceAsset, Path.Combine(root, "Compiled.compiled-library"));

                var plcs = ConfigPlcProjectFactory.PlcProjectsFromPath(root, new PackageServerCollection(), compiled: false).ToList();

                Assert.AreEqual(1, plcs.Count);
                Assert.IsTrue(plcs[0].Plc.FilePath.EndsWith(".library", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(plcs[0].Plc.FilePath.IndexOf(".compiled-library", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        static string NewTempDir()
        {
            var root = Path.Combine(Path.GetTempPath(), "twinpack-frompath-" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
