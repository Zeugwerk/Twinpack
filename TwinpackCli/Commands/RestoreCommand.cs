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
    [Verb("restore", HelpText = @"Restore package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class RestoreCommand : Command
    {
        [Option("include-provided-packages", Required = false, Default = null, HelpText = "Restore packages, which are provided by the configuration (Plcs, which are also packages themselves)")]
        public bool IncludeProvidedPackages { get; set; }
        public IEnumerable<string> Configurations { get; set; }
        [Option("skip-download", Required = false, Default = null, HelpText = "Skips the download of package(s)")]
        public bool SkipDownload { get; set; }
        [Option("force-download", Required = false, Default = null, HelpText = "Forces the download of package(s) even if they are already available on the system.")]
        public bool ForceDownload { get; set; }
        [Option("headed", Required = false, Default = false, HelpText = "Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
        public bool Headed { get; set; }
        public override int Execute()
        {
            if (!ForceDownload && !Headed)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(Headed);

            _twinpack.RestorePackagesAsync(
                new TwinpackService.RestorePackageOptions
                {
                    SkipDownload = SkipDownload,
                    IncludeProvidedPackages = IncludeProvidedPackages,
                    ForceDownload = ForceDownload,
                    IncludeDependencies = true
                }).GetAwaiter().GetResult();

            return 0;
        }


    }
}
