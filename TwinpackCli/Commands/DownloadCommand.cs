using CommandLine;
using NLog.Fluent;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;
using NuGet.Packaging;

namespace Twinpack.Commands
{
    [Verb("download", HelpText = @"Downloads package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class DownloadCommand : Command
    {
        [Option("package", Required = false, HelpText = "Package(s) to handle")]
        public IEnumerable<string> Packages { get; set; }
        [Option("version", Required = false, Default = null, HelpText = "Version(s) of the handled package(s), if not explicitly set, Twinpack downloads the latest version")]
        public IEnumerable<string> Versions { get; set; }
        [Option("branch", Required = false, Default = null, HelpText = "Branches(s) of the handled package(s), if not explicitly set, Twinpack looks up the apporiate option automatically")]
        public IEnumerable<string> Branches { get; set; }
        [Option("target", Required = false, Default = null, HelpText = "Target(s) of the handled package(s), if not explicitly set, Twinpack looks up the apporiate option automatically")]
        public IEnumerable<string> Targets { get; set; }
        [Option("configuration", Required = false, Default = null, HelpText = "Configuration(s) of the handled package(s), if not explicitly set, Twinpack looks up the apporiate option automatically")]
        public IEnumerable<string> Configurations { get; set; }
        [Option("force", Required = false, Default = null, HelpText = "Download packages even if they are already available on the system")]
        public bool ForceDownload { get; set; }
        [Option("headless", Required = false, Default = null, HelpText = "Do not use the Automation Interface, some actions are not available with this option. For instance, using the headless mode will always download packages even if they would already exist on the system, because the Automation Interface can not be used to check if they already exist or not")]
        public bool Headless { get; set; }
        public override int Execute()
        {
            var rootPath = Environment.CurrentDirectory;

            PackagingServerRegistry.InitializeAsync().GetAwaiter().GetResult();

            if (Headless)
            {
                _twinpack = new TwinpackService(PackagingServerRegistry.Servers);
            }
            else
            {
                var config = ConfigFactory.Load(rootPath);

                if (config == null)
                {
                    config = ConfigFactory.CreateFromSolutionFileAsync(
                        rootPath,
                        continueWithoutSolution: false,
                        packageServers: PackagingServerRegistry.Servers.Where(x => x.Connected),
                        plcTypeFilter: null).GetAwaiter().GetResult();
                }

                _twinpack = new TwinpackService(PackagingServerRegistry.Servers, new VisualStudio(hidden: true).Open(config));
            }

            _twinpack.LoginAsync().GetAwaiter().GetResult();

            // create temporary configuration, which holds the packages, which should be downloaded
            List<ConfigPlcPackage> packageConfigs = new List<ConfigPlcPackage>();
            for(int i=0; i < Packages.Count(); i++)
            {
                packageConfigs.Add(new ConfigPlcPackage
                {
                    Name = Packages.ElementAt(i),
                    Version = Versions.ElementAtOrDefault(i) ?? null,
                    Branch = Branches.ElementAtOrDefault(i) ?? null,
                    Target = Targets.ElementAtOrDefault(i) ?? null,
                    Configuration = Configurations.ElementAtOrDefault(i) ?? null
                });
            }

            // get all the information about the configured packages
            var tempConfig = ConfigFactory.Create(null, new List<ConfigProject> { new ConfigProject() }, rootPath);
            tempConfig.Projects[0].Plcs.Add(new ConfigPlcProject { Packages = packageConfigs });
            var packages = _twinpack.RetrieveUsedPackagesAsync(tempConfig, includeMetadata: true).GetAwaiter().GetResult();

            List<PackageItem> downloadedPackageVersions = new List<PackageItem>();

            foreach(var package in packages)
            {
                _twinpack.DownloadPackageAsync(package, downloadedPackageVersions, ForceDownload).GetAwaiter().GetResult();
            }

            foreach (var package in downloadedPackageVersions)
            {
                Console.WriteLine(package.PackageVersion.ToString());
            }

            return 0;
        }
    }
}
