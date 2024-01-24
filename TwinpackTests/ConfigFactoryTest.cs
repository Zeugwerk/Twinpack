using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Models;

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
        public async Task GuessPlcTypeAsyncWithPackageAsync()
        {
            var config = await ConfigFactory.CreateFromSolutionFileAsync(@"assets\TestSolution");
            var plc = config.Projects.FirstOrDefault().Plcs.FirstOrDefault();

            Assert.AreEqual(ConfigPlcProject.PlcProjectType.Application, ConfigPlcProjectFactory.GuessPlcType(plc));

            plc.Packages = plc.Packages.ToList().Append(new ConfigPlcPackage { Name = "TcUnit" });
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
