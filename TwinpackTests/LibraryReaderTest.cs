using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.IO.Compression;

namespace TwinpackTests
{
    [TestClass]
    public class LibraryReaderTest
    {
        const string _loremIpsum = @"Lorem ipsum dolor sit amet, consetetur sadipscing elitr, sed diam nonumy eirmod tempor invidunt ut labore et dolore magna aliquyam erat, sed diam voluptua. At vero eos et accusam et justo duo dolores et ea rebum. Stet clita kasd gubergren, no sea takimata sanctus est Lorem ipsum dolor sit amet. END";

        [DataTestMethod]
        [DataRow(@"assets\LibCat1_All_selected.library", "MyCompany", "MyTitle", "1.0.2.4", "MyDefaultNamespace", "MyAuthor", "MyDescription")]
        [DataRow(@"assets\LibCat4_All_selected.library", "MyCompany", "MyTitle", "5.5.5.5", "", "", "")]
        [DataRow(@"assets\LibCat4_MyParent_selected.library", "MyCompany", "MyTitle", "5.5.5.5", "", "", "")]
        [DataRow(@"assets\LibCat4P_1_selected.library", "MyCompany", "MyTitle", "1.0.2.4", "MyDefaultNamespace", "MyAuthor", "MyDescription")]
        [DataRow(@"assets\Untitled2_libcat.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "MyAuthor", _loremIpsum)]
        [DataRow(@"assets\Untitled2_minimal_infos_libcat_mine_theirs_yours_1234.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled2_minimal_infos_libcat_1234.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled1_libcat_child.library", "MyCompany", "MyTitle", "1.0.2.4", "", "", "")]
        [DataRow(@"assets\Untitled1_libcat_child2.library", "MyCompany", "MyTitle", "1.0.2.4", "MyDefaultNamespace", "MyAuthor", "MyDescription")]
        [DataRow(@"assets\Untitled1_libcat_onlychild_selected.library", "MyCompany", "MyTitle", "1.0.2.4", "MyDefaultNamespace", "MyAuthor", "MyDescription")]
        [DataRow(@"assets\Untitled2_minimal_infos_libcat_mine_1234.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled2_minimal_infos_libcat_mine_theirs_1234.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled2_libcat_1234.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "MyAuthor", _loremIpsum)]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_1234.library", "MyCompany", "MyTitle", "1.2.3.4", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123_n.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "", "")]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123_np.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "", "")]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123_npa.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "MyAuthor", "")]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123_npad.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "MyAuthor", _loremIpsum)]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123_rnpad.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "MyAuthor", _loremIpsum)]
        [DataRow(@"assets\Untitled1_4024_53_minimal_infos_12.library", "MyCompany", "MyTitle", "1.2", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_53_minimal_infos_123.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_53_minimal_infos_1234.library", "MyCompany", "MyTitle", "1.2.3.4", "", "", "")]
        [DataRow(@"assets\Untitled1_4026_00_minimal_infos_123.library", "MyCompany", "MyTitle", "1.0.0", "", "", "")]
        public void TestRead(string file, string company, string title, string version, string defaultNamespace, string author, string description)
        {
            var library = File.ReadAllBytes(file);
            var libraryInfo = Twinpack.LibraryReader.Read(library, file);

            Assert.IsNotNull(library);
            Assert.AreEqual(company, libraryInfo.Company);
            Assert.AreEqual(title, libraryInfo.Title);
            Assert.AreEqual(version, libraryInfo.Version);
            Assert.AreEqual(defaultNamespace, libraryInfo.DefaultNamespace);
            Assert.AreEqual(author, libraryInfo.Author);
            Assert.AreEqual(description, libraryInfo.Description);
        }

        [DataTestMethod]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_1234.library", "MyCompany", "MyTitle", "1.2.3.4", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_35_minimal_infos_123_n.library", "MyCompany", "MyTitle", "1.2.3", "MyDefaultNamespace", "", "")]
        public void ReadProjectInformationBin(string file, string company, string title, string version, string defaultNamespace, string author, string description)
        {
            var library = File.ReadAllBytes(file);
            using (var memoryStream = new MemoryStream(library))
            using (var archive = new ZipArchive(memoryStream))
            {
                var stringTable = Twinpack.LibraryReader.ReadStringTable(archive, file);
                var libraryInfo = Twinpack.LibraryReader.ReadProjectInformationBin(archive, stringTable, file);

                Assert.IsNotNull(library);
                Assert.AreEqual(company, libraryInfo.Company);
                Assert.AreEqual(title, libraryInfo.Title);
                Assert.AreEqual(version, libraryInfo.Version);
                Assert.AreEqual(defaultNamespace, libraryInfo.DefaultNamespace);
                Assert.AreEqual(author, libraryInfo.Author);
                Assert.AreEqual(description, libraryInfo.Description);
            }
        }

        [DataTestMethod]
        [DataRow(@"assets\Untitled1_4024_53_minimal_infos_12.library", "MyCompany", "MyTitle", "1.2", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_53_minimal_infos_123.library", "MyCompany", "MyTitle", "1.2.3", "", "", "")]
        [DataRow(@"assets\Untitled1_4024_53_minimal_infos_1234.library", "MyCompany", "MyTitle", "1.2.3.4", "", "", "")]
        [DataRow(@"assets\Untitled1_4026_00_minimal_infos_123.library", "MyCompany", "MyTitle", "1.0.0", "", "", "")]
        public void ReadProjectInformationXml(string file, string company, string title, string version, string defaultNamespace, string author, string description)
        {
            var library = File.ReadAllBytes(file);
            using (var memoryStream = new MemoryStream(library))
            using (var archive = new ZipArchive(memoryStream))
            {
                var stringTable = Twinpack.LibraryReader.ReadStringTable(archive, file);
                var libraryInfo = Twinpack.LibraryReader.ReadProjectInformationXml(archive, stringTable, file);

                Assert.IsNotNull(library);
                Assert.AreEqual(company, libraryInfo.Company);
                Assert.AreEqual(title, libraryInfo.Title);
                Assert.AreEqual(version, libraryInfo.Version);
                Assert.AreEqual(defaultNamespace, libraryInfo.DefaultNamespace);
                Assert.AreEqual(author, libraryInfo.Author);
                Assert.AreEqual(description, libraryInfo.Description);
            }
        }
    }
}
