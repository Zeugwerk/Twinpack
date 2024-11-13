using System.Collections.Generic;
using Twinpack.Core;
using System.ComponentModel;
using Spectre.Console.Cli;

namespace Twinpack.Commands
{
    [Description(@"Restore package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class RestoreCommand : AbstractCommand<RestoreCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--include-provided-packages")]
            [Description("Restore packages, which are provided by the configuration (Plcs, which are also packages themselves)")]
            public bool IncludeProvidedPackages { get; set; }
            public string[] Configurations { get; set; }
            [CommandOption("--skip-download")]
            [Description("Skips the download of package(s)")]
            public bool SkipDownload { get; set; }
            [CommandOption("--skip-install")]
            [Description("Skips the installation of package(s)")]
            public bool SkipInstall { get; set; }
            [CommandOption("--force-download")]
            [Description("Forces the download of package(s) even if they are already available on the system.")]
            public bool ForceDownload { get; set; }
            [CommandOption("--headed")]
            [Description("Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
            public bool Headed { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            if (!settings.ForceDownload && !settings.Headed)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(settings.Headed);

            _twinpack.RestorePackagesAsync(
                new TwinpackService.RestorePackageOptions
                {
                    SkipDownload = settings.SkipDownload,
                    IncludeProvidedPackages = settings.IncludeProvidedPackages,
                    ForceDownload = settings.ForceDownload,
                    IncludeDependencies = true
                }).GetAwaiter().GetResult();

            return 0;
        }


    }
}
