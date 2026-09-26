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

        /// <summary>
        /// The shape a library published to a NuGet server comes back as. NuGet keeps all four parts,
        /// and packages published under the superseded convention carry the revision in the prerelease,
        /// so either one has to compare equal to the four part version TwinCAT registered the library
        /// under.
        /// </summary>
        [DataTestMethod]
        [DataRow("1.4.2.4", "1.4.2.4", true)]
        [DataRow("1.4.2.4-feat-ci", "1.4.2.4", true)]
        [DataRow("1.4.2.0", "1.4.2-0", true)]
        [DataRow("1.4.2-0", "1.4.2.0", true)]
        [DataRow("1.4.2.0", "1.4.2.0", true)]
        [DataRow("1.4.2.1", "1.4.2-1", true)]
        [DataRow("1.4.2.0-feat-ci", "1.4.2-feat-ci.0", true)]
        [DataRow("1.4.2.1", "1.4.2-0", false)]
        [DataRow("1.4.2.0", "1.4.3-0", false)]
        public void TwinCATLibraryVersionsEqualMatchesAPackageVersion(string left, string right, bool expected)
        {
            Assert.AreEqual(expected, AutomationInterface.TwinCATLibraryVersionsEqual(left, right));
        }

        /// <summary>
        /// NuGet keeps all four parts of a version, so there is nothing to undo. A three part version is
        /// left alone rather than padded, because TwinCAT accepts three part library versions. Packages
        /// published under the superseded convention that put the revision in the prerelease still have
        /// to be read the way that convention meant them.
        /// </summary>
        [DataTestMethod]
        [DataRow("0.1.2.4", "0.1.2.4")]
        [DataRow("0.1.2.4-feat-ci", "0.1.2.4-feat-ci")]
        [DataRow("0.1.2", "0.1.2")]
        [DataRow("0.1.2-feat-ci", "0.1.2-feat-ci")]
        [DataRow("0.1.2-1", "0.1.2.1")]
        [DataRow("0.1.2-feat-ci.1", "0.1.2.1-feat-ci")]
        [DataRow("0.1.2.1-feat-ci", "0.1.2.1-feat-ci")]
        [DataRow("0.1.2.0-rc.1", "0.1.2.0-rc.1")]
        [DataRow("*", "*")]
        [DataRow(null, null)]
        public void FourPartVersionDecodesTheSupersededRevisionConvention(string input, string expected)
        {
            Assert.AreEqual(expected, AutomationInterface.FourPartVersion(input));
        }

        /// <summary>
        /// A library versioned <c>x.y.z.0</c> is published as <c>x.y.z</c> and is then the same string as
        /// a library versioned <c>x.y.z</c>, which TwinCAT also allows. Neither can be turned into the
        /// other, so the two have to be treated as the same version when installed libraries are searched.
        /// </summary>
        [DataTestMethod]
        [DataRow("1.2.3", "1.2.3.0", true)]
        [DataRow("1.2.3.0", "1.2.3", true)]
        [DataRow("1.2.3.0", "1.2.3.0", true)]
        [DataRow("1.2.3", "1.2.3", true)]
        [DataRow("1.2.3-feat-ci", "1.2.3.0", true)]
        [DataRow("1.2.3", "1.2.3.1", false)]
        [DataRow("1.2.3", "1.2.4", false)]
        [DataRow("1.2.3", "not-a-version", false)]
        [DataRow("not-a-version", "1.2.3", false)]
        [DataRow("*", "1.2.3", true)]
        public void TwinCATLibraryVersionsEquivalentTreatsAMissingRevisionAsZero(string left, string right, bool expected)
        {
            Assert.AreEqual(expected, AutomationInterface.TwinCATLibraryVersionsEquivalent(left, right));
        }

        [DataTestMethod]
        [DataRow("feat-ci")]
        [DataRow("1.0.0.0.feat.ci")]
        public void RejectsInvalidVersions(string input)
        {
            Assert.ThrowsException<ArgumentException>(() => AutomationInterface.NormalizedVersion(input));
        }

        [DataTestMethod]
        [DataRow("1.2.3.4", "1.2.3.4")]
        [DataRow("v1.2.3.4", "1.2.3.4")]
        [DataRow("1.2.3.0", "1.2.3.0")]
        [DataRow("1.2.3.4-feat-ci", "1.2.3.4-feat-ci")]
        [DataRow("1.2.3.0-feat-ci", "1.2.3.0-feat-ci")]
        public void NugetVersionKeepsAllFourParts(string input, string expected)
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
        /// A package we pack has to be resolvable back to the version the project asked for. NuGet is
        /// what puts the version into the nuspec and it normalizes a revision of 0 away, so the version
        /// that comes back is not always the same string, only the same version.
        /// </summary>
        [DataTestMethod]
        [DataRow("1.2.3.4", "1.2.3.4")]
        [DataRow("1.2.3.4-feat-ci", "1.2.3.4-feat-ci")]
        [DataRow("0.1.2.11-release-1-x", "0.1.2.11-release-1-x")]
        [DataRow("1.2.3.0", "1.2.3")]
        [DataRow("1.2.3.0-feat-ci", "1.2.3-feat-ci")]
        public void NugetVersionRoundTripsThroughWhatNugetPublishes(string twincatVersion, string publishedVersion)
        {
            var nuspecVersion = NuGetVersion.Parse(AutomationInterface.NugetVersion(twincatVersion));

            Assert.AreEqual(publishedVersion, nuspecVersion.ToFullString());
            Assert.IsTrue(AutomationInterface.TwinCATLibraryVersionsEquivalent(
                twincatVersion,
                AutomationInterface.FourPartVersion(nuspecVersion.ToFullString())));
        }

        /// <summary>
        /// A library with a revision of 0 is published as a plain three part version, which matters
        /// because that is a stable release: encoding the 0 instead would make every released library a
        /// prerelease that a consumer asking for the latest stable version could not resolve.
        /// </summary>
        [DataTestMethod]
        [DataRow("1.2.3.0", "1.2.3")]
        [DataRow("1.2.3.4", "1.2.3.4")]
        public void AReleasedLibraryIsPublishedAsAStableVersion(string twincatVersion, string publishedVersion)
        {
            var nuspecVersion = NuGetVersion.Parse(AutomationInterface.NugetVersion(twincatVersion));

            Assert.AreEqual(publishedVersion, nuspecVersion.ToFullString());
            Assert.IsFalse(nuspecVersion.IsPrerelease);
        }
    }
}
