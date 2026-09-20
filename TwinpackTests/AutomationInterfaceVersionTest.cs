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
        public void AcceptsFourPartPrereleaseAndLegacyDashBuild(string input, string normalized, string numeric)
        {
            Assert.AreEqual(normalized, AutomationInterface.NormalizedVersion(input));
            Assert.AreEqual(numeric, AutomationInterface.TwincatNumericVersion(AutomationInterface.NormalizedVersion(input)));
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
