using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Models;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class ConfigFactoryTest
    {
        [TestMethod]
        public async Task CreateFromSolutionFileWithoutFilterAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");

            Assert.AreEqual(@"assets\TestSolution", config.WorkingDirectory);
            Assert.AreEqual(@"TestSolution.sln", config.Solution);
            Assert.AreEqual(@"assets\TestSolution\.Zeugwerk\config.json", config.FilePath);
            Assert.AreEqual(2, config.Projects.Count);

            var project = config.Projects.Where(x => x.Name == "TestProject").FirstOrDefault();
            Assert.AreEqual(@"TestProject", project?.Name);
            Assert.AreEqual(1, project?.Plcs.Count);

            var plc = project.Plcs.FirstOrDefault();
            Assert.AreEqual(@"Plc1", plc?.Name);
            Assert.AreEqual(@"Plc1", plc?.Title);
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Application, plc?.PlcType);
            Assert.AreEqual(1, plc?.References.Count);
            Assert.AreEqual("1.0.0.0", plc?.Version);
            Assert.AreEqual(@"*", plc?.References?.FirstOrDefault().Key);
            Assert.AreEqual(3, plc?.References?.FirstOrDefault().Value.Count);

            var references = plc?.References?.FirstOrDefault().Value;
            Assert.AreEqual(@"Tc2_Standard=*", references[0]);
            Assert.AreEqual(@"Tc2_System=*", references[1]);
            Assert.AreEqual(@"Tc3_Module=*", references[2]);
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public async Task CreateFromSolution_DirectoryNotFound_Async(bool continueWithoutSolution)
        {
            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(async () => await ConfigFactory.CreateFromSolutionFileAsync(@"assets\NoSuchDirectory", continueWithoutSolution));
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public async Task CreateFromSolution_NoFiles_Async(bool continueWithoutSolution)
        {
            if (!Directory.Exists(@"assets\NoSolutionInside"))
                Directory.CreateDirectory(@"assets\NoSolutionInside");

            Assert.IsNull(await ConfigFactory.CreateFromSolutionFileAsync(@"assets\NoSolutionInside", continueWithoutSolution));
        }

        [TestMethod]
        public async Task CreateFromSolutionFileWithFilterAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution",
                plcTypeFilter: new List<ConfigPlcProject.PlcProjectType> { ConfigPlcProject.PlcProjectType.Library });

            Assert.AreEqual(@"assets\TestSolution", config.WorkingDirectory);
            Assert.AreEqual(@"TestSolution.sln", config.Solution);
            Assert.AreEqual(@"assets\TestSolution\.Zeugwerk\config.json", config.FilePath);
            Assert.AreEqual(1, config.Projects.Count);

            var project = config.Projects.FirstOrDefault();
            Assert.AreEqual(@"TestProject2", project?.Name);
            Assert.AreEqual(1, project?.Plcs.Count);

            var plc = project.Plcs.FirstOrDefault();
            Assert.AreEqual(@"TestProject2", plc?.ProjectName);
            Assert.AreEqual(@"PlcLibrary1", plc?.Name);
            Assert.AreEqual(@"PlcLibrary1", plc?.Title);
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Library, plc?.PlcType);
            Assert.AreEqual(1, plc?.References.Count);
            Assert.AreEqual("1.2.3.4", plc?.Version);
            Assert.AreEqual(@"*", plc?.References?.FirstOrDefault().Key);
            Assert.AreEqual(2, plc?.References?.FirstOrDefault().Value.Count);

            var references = plc?.References?.FirstOrDefault().Value;
            Assert.AreEqual(@"Tc2_Standard=*", references[0]);
            Assert.AreEqual(@"Tc2_System=*", references[1]);
        }

        [TestMethod]
        public async Task CreateFromSolutionFile_SolutionPath_LoadsNamedSolutionAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution\TestSolution.sln");

            Assert.AreEqual(Path.GetFullPath(@"assets\TestSolution"), config.WorkingDirectory);
            Assert.AreEqual(@"TestSolution.sln", config.Solution);
            Assert.AreEqual(Path.Combine(Path.GetFullPath(@"assets\TestSolution"), ".Zeugwerk", "config.json"), config.FilePath);
            Assert.AreEqual(2, config.Projects.Count);
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public async Task CreateFromSolution_SolutionFileNotFound_Async(bool continueWithoutSolution)
        {
            // A named .sln that is not on disk must not fall through to directory discovery — that would
            // quietly load whatever other solution happens to sit next to it.
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () => await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution\NoSuch.sln", continueWithoutSolution));
        }

        [TestMethod]
        public async Task CreateFromSolutionFile_TwoSolutionsInOneFolder_EachLoadsItsOwnProjectsAsync()
        {
            EnsureMultiSolutionFixture();

            var first = await ConfigFactory.CreateFromSolutionFileAsync(Path.Combine(MultiSolutionDir, "First.sln"));
            var second = await ConfigFactory.CreateFromSolutionFileAsync(Path.Combine(MultiSolutionDir, "Second.sln"));

            Assert.AreEqual(@"First.sln", first.Solution);
            Assert.AreEqual(@"TestProject", first.Projects.Single().Name);
            Assert.AreEqual(@"Plc1", first.Projects.Single().Plcs.Single().Name);

            Assert.AreEqual(@"Second.sln", second.Solution);
            Assert.AreEqual(@"TestProject2", second.Projects.Single().Name);
            Assert.AreEqual(@"PlcLibrary1", second.Projects.Single().Plcs.Single().Name);
        }

        [TestMethod]
        public async Task CreateFromSolutionFile_TwoSolutionsInOneFolder_DirectoryStillPicksOneAsync()
        {
            EnsureMultiSolutionFixture();

            var config = await ConfigFactory.CreateFromSolutionFileAsync(MultiSolutionDir);

            // Directory input is unchanged: still first-only, never a merge of both solutions. Which of the
            // two wins is filesystem enumeration order, so only the count is asserted.
            CollectionAssert.Contains(new[] { "First.sln", "Second.sln" }, config.Solution);
            Assert.AreEqual(1, config.Projects.Count);
        }

        private static readonly string MultiSolutionDir = Path.Combine("assets", "MultiSolution");

        /// <summary>
        /// Two solutions side by side in one folder — the shape that first-only discovery cannot express.
        /// Written at test time (like <c>NoSolutionInside</c>) so the fixture needs no csproj copy plumbing;
        /// it points at the TestSolution projects, which are already copied to the output directory.
        /// </summary>
        private static void EnsureMultiSolutionFixture()
        {
            Directory.CreateDirectory(MultiSolutionDir);
            File.WriteAllText(Path.Combine(MultiSolutionDir, "First.sln"),
                SolutionText("TestProject", @"..\TestSolution\TestProject\TestProject.tsproj"));
            File.WriteAllText(Path.Combine(MultiSolutionDir, "Second.sln"),
                SolutionText("TestProject2", @"..\TestSolution\TestProject2\TestProject2.tspproj"));
        }

        private static string SolutionText(string projectName, string projectPath) =>
            "Microsoft Visual Studio Solution File, Format Version 12.00\r\n" +
            "# TcXaeShell Solution File, Format Version 11.00\r\n" +
            "Project(\"{B1E792BE-AA5F-4E3C-8C82-674BF9C0715B}\") = \"" + projectName + "\", \"" + projectPath + "\", \"{FBF99FD5-3861-429A-AA44-FD661631289F}\"\r\n" +
            "EndProject\r\n";

        [TestMethod]
        public async Task GuessPlcTypeAsyncWithPackageAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");
            var plc = config.Projects.FirstOrDefault().Plcs.FirstOrDefault();

            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Application, ConfigPlcProjectFactory.GuessPlcType(plc));

            plc.Packages.Add(new ConfigPlcPackage { Name = "TcUnit" });
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.UnitTestApplication, ConfigPlcProjectFactory.GuessPlcType(plc));

        }

        [TestMethod]
        public async Task GuessPlcTypeAsyncWithReferenceAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");
            var plc = config.Projects.FirstOrDefault().Plcs.FirstOrDefault();

            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Application, ConfigPlcProjectFactory.GuessPlcType(plc));

            plc.References["*"] = plc.References["*"].Append("TcUnit=*").ToList();
            Assert.AreEqual(ConfigPlcProject.PlcProjectType.UnitTestApplication, ConfigPlcProjectFactory.GuessPlcType(plc));

        }
    }
}
