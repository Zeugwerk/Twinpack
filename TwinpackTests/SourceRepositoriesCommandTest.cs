using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Commands;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    [DoNotParallelize]
    public class SourceRepositoriesCommandTest
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
        public async Task ReadOnlyInit_LoadsExistingFileWithoutRewritingIt()
        {
            var directory = CreateWorkspace(enabledPrivate: true);
            Environment.CurrentDirectory = directory;
            var path = Path.Combine(directory, "sourceRepositories.json");
            var before = File.ReadAllText(path);

            await PackagingServerRegistry.InitializeAsync(fallbackToDefaults: true, login: false, saveDefaults: false);

            Assert.AreEqual(before, File.ReadAllText(path));
            var servers = PackagingServerRegistry.Servers.ToList();
            Assert.AreEqual(3, servers.Count);
            Assert.IsFalse(servers.Single(x => x.Name == "twinpack.dev").Enabled);
            Assert.IsTrue(servers.Single(x => x.Name == "private").Enabled);
            Assert.AreEqual("NuGet Repository", servers.Single(x => x.Name == "private").ServerType);
        }

        [TestMethod]
        public async Task UseDefaults_SavesOnlyTheBuiltInFeeds()
        {
            var directory = CreateWorkspace(enabledPrivate: true);
            Environment.CurrentDirectory = directory;
            var path = Path.Combine(directory, "sourceRepositories.json");

            await PackagingServerRegistry.InitializeAsync(useDefaults: true, login: false);

            var text = File.ReadAllText(path);
            StringAssert.Contains(text, "twinpack.dev");
            StringAssert.Contains(text, "beckhoff-cloud.com");
            Assert.IsFalse(text.Contains("nuget.example.invalid"), "reset must drop repositories that were only in the previous file");
        }

        [TestMethod]
        public void NugetPackWithoutConfig_DoesNotRewriteSourceRepositories()
        {
            var directory = CreateWorkspace();
            Environment.CurrentDirectory = directory;
            var path = Path.Combine(directory, "sourceRepositories.json");
            var before = File.ReadAllText(path);

            var command = new NugetPackCommand();
            var error = Assert.ThrowsException<Exception>(() =>
                command.Execute(null, new NugetPackCommand.Settings
                {
                    Quiet = true,
                    WithoutConfig = true,
                    LibraryPath = directory
                }));

            StringAssert.Contains(error.Message, "Could not locate any artifacts");
            Assert.AreEqual(before, File.ReadAllText(path));
            Assert.IsTrue(PackagingServerRegistry.Servers.Any(x => x.Name == "private"));
        }

        [TestMethod]
        public void Pull_DoesNotRewriteSourceRepositories()
        {
            var directory = CreateWorkspace(includeConfig: true);
            Environment.CurrentDirectory = directory;
            var path = Path.Combine(directory, "sourceRepositories.json");
            var before = File.ReadAllText(path);

            var command = new PullCommand();
            var exitCode = command.Execute(null, new PullCommand.Settings { Quiet = true });

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(before, File.ReadAllText(path));
            Assert.IsTrue(PackagingServerRegistry.Servers.Any(x => x.Name == "private"));
            Assert.IsFalse(PackagingServerRegistry.Servers.Single(x => x.Name == "twinpack.dev").Enabled);
        }

        [TestMethod]
        public void PushWithoutConfig_DoesNotRewriteSourceRepositories()
        {
            var directory = CreateWorkspace();
            Environment.CurrentDirectory = directory;
            var path = Path.Combine(directory, "sourceRepositories.json");
            var before = File.ReadAllText(path);

            var command = new PushCommand();
            var error = Assert.ThrowsException<Exception>(() =>
                command.Execute(null, new PushCommand.Settings
                {
                    Quiet = true,
                    WithoutConfig = true,
                    LibraryPath = directory
                }));

            StringAssert.Contains(error.Message, "Could not locate any artifacts");
            Assert.AreEqual(before, File.ReadAllText(path));
            Assert.IsTrue(PackagingServerRegistry.Servers.Any(x => x.Name == "private"));
        }

        static string CreateWorkspace(bool includeConfig = false, bool enabledPrivate = false)
        {
            var directory = Path.Combine(Path.GetTempPath(), "twinpack-sources-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "sourceRepositories.json"), @"{
  ""repositories"": [
    {
      ""name"": ""twinpack.dev"",
      ""serverType"": ""Twinpack Repository"",
      ""url"": ""https://twinpack.dev/api/v1"",
      ""enabled"": false
    },
    {
      ""name"": ""Beckhoff stable"",
      ""serverType"": ""Beckhoff Repository"",
      ""url"": ""https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable"",
      ""enabled"": false
    },
    {
      ""name"": ""private"",
      ""serverType"": ""NuGet Repository"",
      ""url"": ""https://nuget.example.invalid/v3/index.json"",
      ""enabled"": " + (enabledPrivate ? "true" : "false") + @"
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
          ""packages"": []
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
