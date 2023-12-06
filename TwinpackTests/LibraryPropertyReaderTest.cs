using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace TwinpackTests
{
    [TestClass]
    public class LibraryPropertyReaderTest
    {
        [TestMethod]
        public void TestWithoutPlaceholder()
        {
            var library = File.ReadAllBytes(@"assets\Untitled1_WithoutPlaceholder.library");
            var libraryInfo = Twinpack.LibraryPropertyReader.Read(library);

            Assert.IsNotNull(library);
            Assert.AreEqual("MyCompany", libraryInfo.Company);
            Assert.AreEqual("MyLibrary", libraryInfo.Title);
            Assert.AreEqual("6.7.8.9", libraryInfo.Version);
            Assert.AreEqual("MyDescription", libraryInfo.Description);
            Assert.AreEqual("MyAuthor", libraryInfo.Author);
            Assert.AreEqual("MyDefaultNamespace", libraryInfo.DefaultNamespace);
        }

        [TestMethod]
        public void TestWithPlaceholder()
        {
            var library = File.ReadAllBytes(@"assets\Untitled1_WithPlaceholder.library");
            var libraryInfo = Twinpack.LibraryPropertyReader.Read(library);

            Assert.IsNotNull(library);
            Assert.AreEqual("MyCompany", libraryInfo.Company);
            Assert.AreEqual("MyLibrary", libraryInfo.Title);
            Assert.AreEqual("6.7.8.9", libraryInfo.Version);
            Assert.AreEqual("MyDescription", libraryInfo.Description);
            Assert.AreEqual("MyAuthor", libraryInfo.Author);
            Assert.AreEqual("MyDefaultNamespace", libraryInfo.DefaultNamespace);
            Assert.AreEqual(9, libraryInfo.Dependencies.Count);
            Assert.AreEqual("Beckhoff Automation GmbH", libraryInfo.Dependencies[3].DistributorName);
            Assert.AreEqual("*", libraryInfo.Dependencies[3].Version);
            Assert.AreEqual("Tc3_Module", libraryInfo.Dependencies[3].Name);
        }
    }
}
