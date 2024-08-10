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
        [Option("download-provided", Required = false, Default = null, HelpText = "Download packages, which are provided by the configuration (Plcs, which are also packages themselves)")]
        public bool DownloadProvided { get; set; }
        [Option("headed", Required = false, Default = false, HelpText = "Use Beckhoff Automation Interface, some actions are not available without this argument")]
        public bool Headed { get; set; }
        public override int Execute()
        {
            if (!ForceDownload && !Headed)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(Headed);

            var packages = CreatePackageItems(Packages, Versions, Branches, Targets, Configurations);

            // download packages
            var downloadedPackageVersions = _twinpack.DownloadPackagesAsync(packages, downloadProvided: DownloadProvided, includeDependencies: true, ForceDownload).GetAwaiter().GetResult();

            // visualize
            foreach (var package in downloadedPackageVersions)
                Console.WriteLine(package.PackageVersion.ToString());

            return 0;
        }


    }
}
