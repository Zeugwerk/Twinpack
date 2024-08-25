using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Twinpack.Core;

namespace Twinpack.Commands
{
    [Description(@"Downloads package(s) from the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class DownloadCommand : AbstractCommand<DownloadCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandOption("--package")]
            [Description("Specifies the package(s) to be downloaded. If not provided, all relevant packages will be handled.")]
            public string[] Packages { get; set; }
            [CommandOption("--version")]
            [Description("Specifies the version(s) of the package(s) to be downloaded. If not explicitly set, Twinpack downloads the latest version.")]
            public string[] Versions { get; set; }
            [CommandOption("--branch")]
            [Description("Specifies the branch(es) of the package(s). If not provided, Twinpack automatically determines the appropriate branch.")]
            public string[] Branches { get; set; }
            [CommandOption("--target")]
            [Description("Specifies the target(s) for the package(s). If omitted, Twinpack will automatically select the appropriate target.")]
            public string[] Targets { get; set; }
            [CommandOption("--configuration")]
            [Description("Specifies the configuration(s) of the package(s). If not set, Twinpack will automatically select the appropriate configuration.")]
            public string[] Configurations { get; set; }
            [CommandOption("--force-download")]
            [Description("Forces the download of package(s) even if they are already available on the system.")]
            public bool ForceDownload { get; set; }
            [CommandOption("--include-provided-packages")]
            [Description("Includes the download of packages that are provided by the configuration (e.g., PLCs that are also considered packages by being available on a configured package server).")]
            public bool IncludeProvidedPackages { get; set; }
            [CommandOption("--headed")]
            [Description("Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
            public bool Headed { get; set; }
        }
        public override int Execute(CommandContext context, Settings settings)
        {
            if (!settings.ForceDownload && !settings.Headed)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(settings.Headed);

            var packages = CreatePackageItems(settings.Packages, settings.Versions, settings.Branches, settings.Targets, settings.Configurations);

            if (settings.Packages == null)
                packages = _twinpack.RetrieveUsedPackagesAsync().GetAwaiter().GetResult().ToList();

            // download packages
            var downloadedPackageVersions = _twinpack.DownloadPackagesAsync(packages,
                new TwinpackService.DownloadPackageOptions
                {
                    IncludeProvidedPackages = settings.IncludeProvidedPackages, 
                    IncludeDependencies = true, 
                    ForceDownload = settings.ForceDownload,
                }).GetAwaiter().GetResult();

            return 0;
        }
    }
}
