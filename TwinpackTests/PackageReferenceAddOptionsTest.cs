using Microsoft.VisualStudio.TestTools.UnitTesting;
using Twinpack.Models;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class PackageReferenceAddOptionsTest
    {
        [TestMethod]
        public void CopyOptionForDependency()
        {
            var options = new PackageReferenceAddOptions
            {
                LibraryReference = true,
                Optional = true,
                HideWhenReferencedAsDependency = true,
                PublishSymbolsInContainer = true,
                QualifiedOnly = true,
            };

            var dependencyOption = options.CopyForDependency();

            Assert.AreEqual(false, dependencyOption.LibraryReference);
            Assert.AreEqual(false, dependencyOption.Optional);
            Assert.AreEqual(false, dependencyOption.HideWhenReferencedAsDependency);
            Assert.AreEqual(false, dependencyOption.PublishSymbolsInContainer);
            Assert.AreEqual(true, dependencyOption.QualifiedOnly);
        }
    }
}
