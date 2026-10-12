using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class PlcLibraryPackageTest
    {
        [TestMethod]
        public void BrowserCacheNamesTheLibraryToReference()
        {
            var root = Path.Combine(Path.GetTempPath(), "plc-lib-" + Path.GetRandomFileName());
            var mc3 = Path.Combine(root, "Beckhoff Automation GmbH", "Tc3_Mc3Base", "4.0.17.0");
            var other = Path.Combine(root, "Beckhoff Automation GmbH", "Tc3_Mc3Extra", "1.0.0.0");
            Directory.CreateDirectory(mc3);
            Directory.CreateDirectory(other);
            try
            {
                File.WriteAllText(Path.Combine(mc3, "browsercache"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Library Name=\"Tc3_Mc3Base, 4.0.17.0 (Beckhoff Automation GmbH)\" />");
                File.WriteAllText(Path.Combine(other, "browsercache"),
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Library Name=\"Tc3_Mc3Extra, 1.0.0.0 (Beckhoff Automation GmbH)\" />");
                var mc3File = Path.Combine(mc3, "Tc3_Mc3Base.library");
                var extraFile = Path.Combine(other, "Tc3_Mc3Extra.library");
                File.WriteAllBytes(mc3File, new byte[] { 0 });
                File.WriteAllBytes(extraFile, new byte[] { 0 });

                var libraries = PlcLibraryPackage.DescribeAll(new[] { mc3File, extraFile });
                var reference = PlcLibraryPackage.ChooseReference(
                    libraries,
                    "TwinCAT.XAE.PLC.Lib.Motion.MC3.Tc3_Mc3Base",
                    "MC3");

                Assert.AreEqual("Tc3_Mc3Base", reference.Title);
                Assert.AreEqual("4.0.17.0", reference.Version);
                Assert.AreEqual("Beckhoff Automation GmbH", reference.Distributor);
                Assert.IsFalse(reference.Compiled);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ProjectInfoIsUsedWhenBrowserCacheIsMissing()
        {
            var root = Path.Combine(Path.GetTempPath(), "plc-lib-" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "projectinfo"), "TcLibraryId\0Beckhoff Automation GmbH|Tc2_BACnetRev12|2.6.6.0");
                var file = Path.Combine(root, "Tc2_BACnetRev12.compiled-library");
                File.WriteAllBytes(file, new byte[] { 0 });

                var library = PlcLibraryPackage.Describe(file);

                Assert.AreEqual("Tc2_BACnetRev12", library.Title);
                Assert.AreEqual("2.6.6.0", library.Version);
                Assert.AreEqual("Beckhoff Automation GmbH", library.Distributor);
                Assert.IsTrue(library.Compiled);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void RuntimeFilesAreNotLibraries()
        {
            Assert.IsTrue(PlcLibraryPackage.IsLibraryFile("Tc3_Mc3Base.library"));
            Assert.IsTrue(PlcLibraryPackage.IsLibraryFile("Tc2_Standard.compiled-library"));
            Assert.IsTrue(PlcLibraryPackage.IsLibraryFile("Tc2_Standard.compiled-library-ge33"));
            Assert.AreEqual("Tc2_Standard", PlcLibraryPackage.LibraryTitle("Tc2_Standard.compiled-library-ge33"));
            Assert.AreEqual(".compiled-library-ge33", PlcLibraryPackage.LibrarySuffix("Tc2_Standard.compiled-library-ge33"));
            Assert.IsFalse(PlcLibraryPackage.IsLibraryFile("Tc3_Mc3Base.tml"));
            Assert.IsFalse(PlcLibraryPackage.IsLibraryFile("Tc3_Mc3Base.tmx"));
            Assert.IsFalse(PlcLibraryPackage.IsLibraryFile("browsercache"));
        }
    }
}
