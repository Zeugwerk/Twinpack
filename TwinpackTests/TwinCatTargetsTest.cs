using Microsoft.VisualStudio.TestTools.UnitTesting;
using Twinpack.Core;

namespace TwinpackTests
{
    [TestClass]
    public class TwinCatTargetsTest
    {
        [TestMethod]
        public void ProductLine_DropsThePatch()
        {
            Assert.AreEqual("TC3.1.4024", TwinCatTargets.ProductLine("TC3.1.4024.59"));
            Assert.AreEqual("TC3.1.4024", TwinCatTargets.ProductLine("3.1.4024.59"));
            Assert.AreEqual("TC3.1.4026", TwinCatTargets.ProductLine("TC3.1.4026"));
            Assert.AreEqual("TC3.1", TwinCatTargets.ProductLine("TC3.1"));
            Assert.IsNull(TwinCatTargets.ProductLine(null));
        }

        [TestMethod]
        public void ResolveOrder_TriesTheSolutionLineThenTheUniversalPackage()
        {
            CollectionAssert.AreEqual(
                new[] { "TC3.1.4024", "TC3.1" },
                (System.Collections.ICollection)TwinCatTargets.ResolveOrder("TC3.1", "TC3.1.4024.59"));
            CollectionAssert.AreEqual(
                new[] { "TC3.1" },
                (System.Collections.ICollection)TwinCatTargets.ResolveOrder("", null));
        }

        [TestMethod]
        public void ResolveOrder_APinIsExact()
        {
            CollectionAssert.AreEqual(
                new[] { "TC3.1.4026" },
                (System.Collections.ICollection)TwinCatTargets.ResolveOrder("TC3.1.4026", "TC3.1.4024.59"));
        }

        [TestMethod]
        public void TargetToStore_KeepsTheDefaultUntilTheUserPicksALine()
        {
            Assert.AreEqual("TC3.1", TwinCatTargets.TargetToStore("TC3.1", "TC3.1.4026", false));
            Assert.AreEqual("TC3.1.4026", TwinCatTargets.TargetToStore("TC3.1", "TC3.1.4026", true));
            Assert.AreEqual("TC3.1.4024", TwinCatTargets.TargetToStore("TC3.1.4024", "TC3.1.4026", false));
            Assert.AreEqual("TC3.1", TwinCatTargets.TargetToStore("TC3.1.4024", "TC3.1", true));
        }
    }
}