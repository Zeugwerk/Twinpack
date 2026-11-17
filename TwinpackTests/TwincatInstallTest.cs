using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using Twinpack.Configuration;
using Twinpack.Core;

namespace TwinpackTests
{
    [TestClass]
    public class TwincatInstallTest
    {
        [TestMethod]
        public void RootFromBootDir_UsesParentOfBootFolder()
        {
            Assert.AreEqual(@"C:\TwinCAT\3.1", TwincatInstall.RootFromBootDir(@"C:\TwinCAT\3.1\Boot\"));
            Assert.AreEqual(@"C:\ProgramData\Beckhoff\TwinCAT\3.1", TwincatInstall.RootFromBootDir(@"C:\ProgramData\Beckhoff\TwinCAT\3.1\Boot"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void RootFromBootDir_EmptyIsNull(string bootDir)
        {
            Assert.IsNull(TwincatInstall.RootFromBootDir(bootDir));
        }

        [TestMethod]
        public void IsUsableRoot_RejectsDriveRootAndMissingFolders()
        {
            Assert.IsFalse(TwincatInstall.IsUsableRoot(null));
            Assert.IsFalse(TwincatInstall.IsUsableRoot(""));
            Assert.IsFalse(TwincatInstall.IsUsableRoot(@"C:\"));
            Assert.IsFalse(TwincatInstall.IsUsableRoot(@"C:\ThisTwinCATRootDoesNotExist-Twinpack"));
        }

        [TestMethod]
        public void IsUsableLicenseFolder_RejectsUnresolvedCustomConfigAtDriveRoot()
        {
            Assert.IsFalse(TwincatInstall.IsUsableLicenseFolder(@"\CustomConfig\Licenses"));
            Assert.IsFalse(TwincatInstall.IsUsableLicenseFolder(@"C:\CustomConfig\Licenses"));
            Assert.IsFalse(TwincatInstall.IsUsableLicenseFolder(null));
        }

        [TestMethod]
        public void LicenseFolders_OnlyUnderExistingTwinCATRoots()
        {
            var root = Path.Combine(Path.GetTempPath(), "TwinpackTwincatInstallTest", Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            try
            {
                var folders = TwincatInstall.LicenseFolders(new[] { root, @"C:\", null, @"\CustomConfig\Licenses" });

                CollectionAssert.AreEquivalent(
                    new[] { Path.Combine(root, @"CustomConfig\Licenses") },
                    new List<string>(folders));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void ResolveLicenseFolders_DropsBrokenInterfacePathAndKeepsValidOnes()
        {
            var root = Path.Combine(Path.GetTempPath(), "TwinpackTwincatInstallTest", Path.GetRandomFileName());
            var valid = Path.Combine(root, @"CustomConfig\Licenses");
            Directory.CreateDirectory(root);
            try
            {
                var folders = TwincatInstall.ResolveLicenseFolders(@"\CustomConfig\Licenses", new[] { valid, @"C:\CustomConfig\Licenses" });

                CollectionAssert.Contains(new List<string>(folders), Path.GetFullPath(valid));
                CollectionAssert.DoesNotContain(new List<string>(folders), @"C:\CustomConfig\Licenses");
                CollectionAssert.DoesNotContain(new List<string>(folders), @"\CustomConfig\Licenses");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void RepositoryFolders_Includes4024RootAndRegistryDir()
        {
            var fixture = Path.Combine(Path.GetTempPath(), "TwinpackTwincatInstallTest", Path.GetRandomFileName());
            var legacy = Path.Combine(fixture, "TwinCAT", "3.1");
            var programData = Path.Combine(fixture, "ProgramData", "Beckhoff", "TwinCAT", "3.1");
            var programFiles = Path.Combine(fixture, "Program Files (x86)", "Beckhoff", "TwinCAT", "3.1");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(programData);
            Directory.CreateDirectory(programFiles);
            try
            {

                var folders = TwincatInstall.RepositoryFolders(
                    new[] { legacy, programData, programFiles, @"C:\" },
                    new[]
                    {
                        Path.Combine(programData, "Repository") + Path.DirectorySeparatorChar,
                        Path.Combine(programFiles, "Repository")
                    });

                CollectionAssert.Contains(new List<string>(folders), Path.Combine(legacy, "Repository"));
                CollectionAssert.Contains(new List<string>(folders), Path.Combine(programData, "Repository"));
                CollectionAssert.DoesNotContain(new List<string>(folders), Path.Combine(programFiles, "Repository"));
                Assert.AreEqual(2, folders.Count);
            }
            finally
            {
                Directory.Delete(fixture, recursive: true);
            }
        }

        [TestMethod]
        public void DiscoverLicenseFolders_DoesNotIncludeProgramFiles()
        {
            var folders = new List<string>(TwincatInstall.DiscoverLicenseFolders());

            CollectionAssert.DoesNotContain(folders, @"C:\Program Files (x86)\Beckhoff\TwinCAT\3.1\CustomConfig\Licenses");
            CollectionAssert.DoesNotContain(folders, @"C:\Program Files\Beckhoff\TwinCAT\3.1\CustomConfig\Licenses");
            if (Directory.Exists(@"C:\ProgramData\Beckhoff\TwinCAT\3.1"))
                CollectionAssert.Contains(folders, @"C:\ProgramData\Beckhoff\TwinCAT\3.1\CustomConfig\Licenses");
            if (Directory.Exists(@"C:\TwinCAT\3.1"))
                CollectionAssert.Contains(folders, @"C:\TwinCAT\3.1\CustomConfig\Licenses");
        }

        [TestMethod]
        public void LicenseFolders_WritesTmcIntoEveryResolvedFolder()
        {
            var fixture = Path.Combine(Path.GetTempPath(), "TwinpackTwincatInstallTest", Path.GetRandomFileName());
            var root32 = Path.Combine(fixture, "x86");
            var root64 = Path.Combine(fixture, "x64");
            Directory.CreateDirectory(root32);
            Directory.CreateDirectory(root64);
            try
            {
                var folders = TwincatInstall.LicenseFolders(new[] { root32, root64 });
                Assert.AreEqual(2, folders.Count);

                const string fileName = "ZF1010.tmc";
                const string tmc = "<TcModuleClass><Licenses><License><LicenseId>ZF1010</LicenseId></License></Licenses></TcModuleClass>";
                foreach (var folder in folders)
                {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, fileName), tmc);
                }

                foreach (var folder in folders)
                    Assert.IsTrue(File.Exists(Path.Combine(folder, fileName)), folder);
            }
            finally
            {
                Directory.Delete(fixture, recursive: true);
            }
        }

        [TestMethod]
        public void Inherit_CopiesTheParentUnlessAnotherParentIsUnrestricted()
        {
            var only4026 = new ConfigWhen { Target = new List<string> { "[TC3.1.4026,)" } };
            var open = false;
            var inherited = ConfigWhen.Inherit(null, only4026, ref open);
            CollectionAssert.AreEqual(new[] { "[TC3.1.4026,)" }, inherited.Target.ToArray());
            Assert.IsFalse(open);

            var also4024 = new ConfigWhen { Target = new List<string> { "TC3.1.4024" } };
            inherited = ConfigWhen.Inherit(inherited, also4024, ref open);
            CollectionAssert.AreEqual(new[] { "[TC3.1.4026,)", "TC3.1.4024" }, inherited.Target.ToArray());

            inherited = ConfigWhen.Inherit(inherited, null, ref open);
            Assert.IsNull(inherited);
            Assert.IsTrue(open);

            inherited = ConfigWhen.Inherit(inherited, only4026, ref open);
            Assert.IsNull(inherited);
            Assert.IsTrue(open);
        }

        static Config RestrictedMc3()
        {
            return new Config
            {
                Projects = new List<ConfigProject>
                {
                    new ConfigProject
                    {
                        Plcs = new List<ConfigPlcProject>
                        {
                            new ConfigPlcProject
                            {
                                Packages = new List<ConfigPlcPackage>
                                {
                                    new ConfigPlcPackage
                                    {
                                        Name = "Tc3_Mc3Ptp",
                                        When = new ConfigWhen { Target = new List<string> { "[TC3.1.4026,)" } }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        [TestMethod]
        public void UseProductLine_RejectsTc31WhenABuildRestrictionCannotBeChecked()
        {
            var headless = new AutomationInterfaceHeadless(RestrictedMc3());

            var error = Assert.ThrowsException<InvalidOperationException>(() => headless.UseProductLine("TC3.1"));

            StringAssert.Contains(error.Message, "TC3.1");
            StringAssert.Contains(error.Message, "[TC3.1.4026,)");
            StringAssert.Contains(error.Message, "Tc3_Mc3Ptp");
        }

        [TestMethod]
        public void UseProductLine_AcceptsAProductLineAgainstABuildRestriction()
        {
            var headless = new AutomationInterfaceHeadless(RestrictedMc3());

            headless.UseProductLine("TC3.1.4024");
            Assert.AreEqual("TC3.1.4024", headless.SolutionProductLine);

            headless.UseProductLine("TC3.1.4026.14");
            Assert.AreEqual("TC3.1.4026", headless.SolutionProductLine);
        }

        [TestMethod]
        public void UseProductLine_AllowsTc31WhenNothingIsRestricted()
        {
            var headless = new AutomationInterfaceHeadless(new Config());

            headless.UseProductLine("TC3.1");

            Assert.IsNull(headless.SolutionProductLine);
        }

        [TestMethod]
        public void ReferenceAlreadyPresent_MatchesThePlaceholderMessageOnly()
        {
            Assert.IsTrue(AutomationInterface.ReferenceAlreadyPresent(new ArgumentException("Placeholder 'ZCore' already contained!")));
            Assert.IsFalse(AutomationInterface.ReferenceAlreadyPresent(new ArgumentException("An item with the same key has already been added.")));
            Assert.IsFalse(AutomationInterface.ReferenceAlreadyPresent(null));
        }
    }
}
