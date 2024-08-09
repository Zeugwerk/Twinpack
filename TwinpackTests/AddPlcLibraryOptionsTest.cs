using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Models;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class AddPlcLibraryOptionsTest
    {
        [TestMethod]
        public async Task CopyOptionForDependency()
        {
            var options = new AddPlcLibraryOptions
            {
                LibraryReference = true,
                Optional = true,
                HideWhenReferencedAsDependency = true,
                PublishSymbolsInContainer = true,
                QualifiedOnly = true,
            };

            var copy = options.CopyForDependency();

            Assert.AreEqual(false, options.LibraryReference);
            Assert.AreEqual(false, options.Optional);
            Assert.AreEqual(false, options.HideWhenReferencedAsDependency);
            Assert.AreEqual(false, options.PublishSymbolsInContainer);
            Assert.AreEqual(true, options.QualifiedOnly);
        }
    }
}
