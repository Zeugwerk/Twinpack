using CommandLine;
using System;
using System.Collections.Generic;
using Twinpack.Core;

namespace Twinpack.Commands
{
    [Verb("download", HelpText = @"Downloads package(s) from the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class DownloadCommand : Command
    {
        [Option("package", Required = false, HelpText = "Specifies the package(s) to be downloaded. If not provided, all relevant packages will be handled.")]
        public IEnumerable<string> Packages { get; set; }
        [Option("version", Required = false, Default = null, HelpText = "Specifies the version(s) of the package(s) to be downloaded. If not explicitly set, Twinpack downloads the latest version.")]
        public IEnumerable<string> Versions { get; set; }
        [Option("branch", Required = false, Default = null, HelpText = "Specifies the branch(es) of the package(s). If not provided, Twinpack automatically determines the appropriate branch.")]
        public IEnumerable<string> Branches { get; set; }
        [Option("target", Required = false, Default = null, HelpText = "Specifies the target(s) for the package(s). If omitted, Twinpack will automatically select the appropriate target.")]
        public IEnumerable<string> Targets { get; set; }
        [Option("configuration", Required = false, Default = null, HelpText = "Specifies the configuration(s) of the package(s). If not set, Twinpack will automatically select the appropriate configuration.")]
        public IEnumerable<string> Configurations { get; set; }
        [Option("force", Required = false, Default = null, HelpText = "Forces the download of package(s) even if they are already available on the system.")]
        public bool ForceDownload { get; set; }
        [Option("include-provided-packages", Required = false, Default = null, HelpText = "Includes the download of packages that are provided by the configuration (e.g., PLCs that are also considered packages by being available on a configured package server).")]
        public bool IncludeProvidedPackages { get; set; }
        [Option("headed", Required = false, Default = false, HelpText = "Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
        public bool Headed { get; set; }
        public override int Execute()
        {
            if (!ForceDownload && !Headed)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(Headed);

            var packages = CreatePackageItems(Packages, Versions, Branches, Targets, Configurations);

            // download packages
            var downloadedPackageVersions = _twinpack.DownloadPackagesAsync(packages,
                new TwinpackService.DownloadPackageOptions
                {
                    IncludeProvidedPackages = IncludeProvidedPackages, 
                    IncludeDependencies = true, 
                    ForceDownload = ForceDownload,
                }).GetAwaiter().GetResult();

            // visualize
            foreach (var package in downloadedPackageVersions)
                Console.WriteLine(package.PackageVersion.ToString());

            return 0;
        }


    }
}
