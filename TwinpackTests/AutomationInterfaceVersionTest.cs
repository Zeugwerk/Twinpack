using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Versioning;
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

        [DataTestMethod]
        [DataRow("1.2.3.0", "1.2.3")]
        [DataRow("v1.2.3.0", "1.2.3")]
        [DataRow("1.2.3.4", "1.2.3-4")]
        [DataRow("1.2.3.0-feat-ci", "1.2.3-feat-ci")]
        [DataRow("1.2.3.4-feat-ci", "1.2.3-feat-ci.4")]
        public void NugetVersionMovesRevisionIntoPrerelease(string input, string expected)
        {
            Assert.AreEqual(expected, AutomationInterface.NugetVersion(input));
        }

        [DataTestMethod]
        [DataRow("1.2.3")]
        [DataRow("1.2.3-4")]
        [DataRow("1.2.3-feat-ci")]
        [DataRow("*")]
        [DataRow(null)]
        public void NugetVersionLeavesNonFourPartVersionsAlone(string input)
        {
            Assert.AreEqual(input, AutomationInterface.NugetVersion(input));
        }

        /// <summary>
        /// The whole point of <see cref="AutomationInterface.NugetVersion"/> is that a package we
        /// pack can be resolved back to the version the project asked for, so it has to be the exact
        /// inverse of <see cref="AutomationInterface.FourPartVersion"/>.
        /// </summary>
        [DataTestMethod]
        [DataRow("1.2.3.4")]
        [DataRow("1.2.3.4-feat-ci")]
        [DataRow("0.1.2.11-release-1-x")]
        public void NugetVersionRoundTripsThroughFourPartVersion(string twincatVersion)
        {
            Assert.AreEqual(twincatVersion, AutomationInterface.FourPartVersion(AutomationInterface.NugetVersion(twincatVersion)));
        }

        /// <summary>
        /// A revision of 0 is dropped rather than encoded, exactly as `nuget pack` would drop it.
        /// The round trip is therefore version-equal rather than string-equal, which is the same
        /// equivalence NugetServer relies on when it matches a requested version against a feed.
        /// </summary>
        [DataTestMethod]
        [DataRow("1.2.3.0", "1.2.3")]
        [DataRow("1.2.3.0-feat-ci", "1.2.3-feat-ci")]
        public void NugetVersionDropsAZeroRevision(string twincatVersion, string expected)
        {
            var nuget = AutomationInterface.NugetVersion(twincatVersion);
            Assert.AreEqual(expected, nuget);
            Assert.AreEqual(
                NuGetVersion.Parse(twincatVersion),
                NuGetVersion.Parse(AutomationInterface.FourPartVersion(nuget)));
        }
    }
}
