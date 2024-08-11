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
    [Verb("update", HelpText = @"Downloads package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class UpdateCommand : Command
    {
        [Option("include-provided-packages", Required = false, Default = null, HelpText = "Restore packages, which are provided by the configuration (Plcs, which are also packages themselves)")]
        public bool IncludeProvidedPackages { get; set; }
        public IEnumerable<string> Configurations { get; set; }
        [Option("force", Required = false, Default = null, HelpText = "Download packages even if they are already available on the system")]
        public bool ForceDownload { get; set; }
        [Option("headed", Required = false, Default = false, HelpText = "Use Beckhoff Automation Interface, some actions are not available without this argument")]
        public bool Headed { get; set; }
        public override int Execute()
        {
            if (!ForceDownload && !Headed)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(Headed);

            _twinpack.UpdatePackagesAsync(
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = IncludeProvidedPackages,
                    ForceDownload = ForceDownload,
                    IncludeDependencies = true
                }).GetAwaiter().GetResult();
            return 0;
        }


    }
}
