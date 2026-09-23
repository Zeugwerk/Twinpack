using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Twinpack.Core;

namespace TwinpackTests
{
    [TestClass]
    public class AutomationInterfaceVersionTest
    {
        [DataTestMethod]
        [DataRow("1.0.0.0", "1.0.0.0", "1.0.0.0")]
        [DataRow("v1.0.0.0", "1.0.0.0", "1.0.0.0")]
        [DataRow("1.0.0-0", "1.0.0.0", "1.0.0.0")]
        [DataRow("  v1.2.3-4", "1.2.3.4", "1.2.3.4")]
        [DataRow("1.0-feat-ci", "1.0-feat-ci", "1.0")]
        [DataRow("1.0.0-feat-ci", "1.0.0-feat-ci", "1.0.0")]
        [DataRow("1.0.0.0-feat-ci", "1.0.0.0-feat-ci", "1.0.0.0")]
        [DataRow("0.1.2.1-feat-ci", "0.1.2.1-feat-ci", "0.1.2.1")]
        [DataRow("v0.1.2.0-feat-ci", "0.1.2.0-feat-ci", "0.1.2.0")]
        [DataRow("0.1.2-feat-ci.1", "0.1.2-feat-ci.1", "0.1.2.1")]
        public void AcceptsFourPartPrereleaseAndLegacyDashBuild(string input, string normalized, string numeric)
        {
            Assert.AreEqual(normalized, AutomationInterface.NormalizedVersion(input));
            Assert.AreEqual(numeric, AutomationInterface.TwincatNumericVersion(AutomationInterface.NormalizedVersion(input)));
            Assert.AreEqual(numeric, AutomationInterface.TwinCATLibraryVersion(input));
        }

        [TestMethod]
        public void TwinCATLibraryVersionKeepsWildcard()
        {
            Assert.AreEqual("*", AutomationInterface.TwinCATLibraryVersion("*"));
            Assert.IsNull(AutomationInterface.TwinCATLibraryVersion(null));
            Assert.IsTrue(AutomationInterface.TwinCATLibraryVersionsEqual("0.1.1.0-feat-ci", "0.1.1.0"));
            Assert.IsFalse(AutomationInterface.TwinCATLibraryVersionsEqual("0.1.1.0-feat-ci", "0.1.2.0"));
        }

        [DataTestMethod]
        [DataRow("0.1.2", "0.1.2")]
        [DataRow("0.1.2-1", "0.1.2.1")]
        [DataRow("0.1.2-feat-ci", "0.1.2-feat-ci")]
        [DataRow("0.1.2-feat-ci.1", "0.1.2.1-feat-ci")]
        [DataRow("0.1.2.1-feat-ci", "0.1.2.1-feat-ci")]
        [DataRow("0.1.2.0-rc.1", "0.1.2.0-rc.1")]
        public void FourPartVersionMovesRevisionOutOfPrerelease(string input, string expected)
        {
            Assert.AreEqual(expected, AutomationInterface.FourPartVersion(input));
        }

        [DataTestMethod]
        [DataRow("feat-ci")]
        [DataRow("1.0.0.0.feat.ci")]
        public void RejectsInvalidVersions(string input)
        {
            Assert.ThrowsException<ArgumentException>(() => AutomationInterface.NormalizedVersion(input));
        }
    }
}
