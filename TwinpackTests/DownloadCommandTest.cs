using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using Twinpack.Commands;

namespace TwinpackTests
{
    [TestClass]
    [DoNotParallelize]
    public class DownloadCommandTest
    {
        string _previousDirectory;

        [TestInitialize]
        public void RememberWorkingDirectory()
        {
            _previousDirectory = Environment.CurrentDirectory;
        }

        [TestCleanup]
        public void RestoreWorkingDirectory()
        {
            Environment.CurrentDirectory = _previousDirectory;
        }

        [TestMethod]
        public void Download_WithoutPackage_RequiresConfigOrSolution()
        {
            Environment.CurrentDirectory = CreateWorkspace(includeConfig: false);

            var command = new DownloadCommand();
            var error = Assert.ThrowsException<FileNotFoundException>(() =>
                command.Execute(null, new DownloadCommand.Settings { Quiet = true }));

            StringAssert.Contains(error.Message, "config.json");
        }

        [TestMethod]
        public void Download_WithPackage_RunsWithoutConfigOrSolution()
        {
            var directory = CreateWorkspace(includeConfig: false);
            Environment.CurrentDirectory = directory;

            var command = new DownloadCommand();
            var exitCode = command.Execute(null, new DownloadCommand.Settings
            {
                Quiet = true,
                Packages = new[] { "SampleLib" },
                Versions = new[] { "1.0.0.0" }
            });

            Assert.AreEqual(0, exitCode);
            Assert.IsFalse(File.Exists(Path.Combine(directory, ".Zeugwerk", "config.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(directory, ".Zeugwerk", "libraries")));
        }

        [TestMethod]
        public void Download_WithoutPackage_AcceptsConfig()
        {
            Environment.CurrentDirectory = CreateWorkspace(includeConfig: true);

            var command = new DownloadCommand();
            var exitCode = command.Execute(null, new DownloadCommand.Settings { Quiet = true });

            Assert.AreEqual(0, exitCode);
        }

        static string CreateWorkspace(bool includeConfig)
        {
            var directory = Path.Combine(Path.GetTempPath(), "twinpack-download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "sourceRepositories.json"), @"{
  ""repositories"": [
    {
      ""name"": ""offline"",
      ""serverType"": ""Twinpack Repository"",
      ""url"": ""https://127.0.0.1:9"",
      ""enabled"": false
    }
  ]
}");

            if (includeConfig)
            {
                var configDirectory = Path.Combine(directory, ".Zeugwerk");
                Directory.CreateDirectory(configDirectory);
                File.WriteAllText(Path.Combine(configDirectory, "config.json"), @"{
  ""fileversion"": 2,
  ""projects"": [
    {
      ""name"": ""App"",
      ""plcs"": [
        {
          ""name"": ""Plc"",
          ""version"": ""1.0.0.0"",
          ""packages"": [
            { ""name"": ""SampleLib"", ""version"": ""1.0.0.0"" }
          ]
        }
      ]
    }
  ]
}");
            }

            return directory;
        }
    }
}
