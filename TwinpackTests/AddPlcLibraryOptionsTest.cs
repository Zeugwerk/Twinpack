﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            
            var dependencyOption = options.CopyForDependency();

            Assert.AreEqual(false, dependencyOption.LibraryReference);
            Assert.AreEqual(false, dependencyOption.Optional);
            Assert.AreEqual(false, dependencyOption.HideWhenReferencedAsDependency);
            Assert.AreEqual(false, dependencyOption.PublishSymbolsInContainer);
            Assert.AreEqual(true, dependencyOption.QualifiedOnly);
        }
    }
}
