using Spectre.Console.Cli;
using System.ComponentModel;
using static Twinpack.Core.TwinpackService;

namespace Twinpack.Commands
{
    [Description(@"Downloads package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class SetVersionCommand : AbstractCommand<SetVersionCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandArgument(0, "<VERSION>")]
            public string Version { get; set; }

            [CommandOption("--project")]
            [Description("Name of the project, which contains the PLC to set the version for. Defaults to null, meaning 'all projects'")]

            public string? ProjectName { get; set; }

            [CommandOption("--plc")]
            [Description("Name of the plc to set the version for. Defaults to null, meaning 'all plcs'")]

            public string? PlcName { get; set; }

            [CommandOption("--sync-framework-packages")]
            [Description("Set the version also for all other packages, which are part of the same framework")]

            public bool SyncFrameworkPackages { get; set; }
            [CommandOption("--purge-packages")]
            [Description("Purges the PLC from packages, which are not configured")]

            public bool PurgePackages { get; set; }

            [CommandOption("--branch")]
            [Description("Together with 'sync-framework-packages', the preferred branch of framework packages")]

            public string? PreferredFrameworkBranch { get; set; }

            [CommandOption("--target")]
            [Description("Together with 'sync-framework-packages', the preferred target of framework package")]

            public string? PreferredFrameworkTarget { get; set; }

            [CommandOption("--configuration")]
            [Description("Together with 'sync-framework-packages', the preferred configuration of framework packages")]

            public string? PreferredFrameworkConfiguration { get; set; }

            [CommandOption("--headed")]
            [Description("Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
            public bool Headed { get; set; }

        }
        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);
            Initialize(settings.Headed);

            _twinpack.SetPackageVersionAsync(settings.Version, 
                new SetPackageVersionOptions
                {
                    PurgePackages = settings.PurgePackages,
                    ProjectName = settings.ProjectName,
                    PlcName = settings.PlcName,
                    SyncFrameworkPackages = settings.SyncFrameworkPackages,
                    PreferredFrameworkBranch = settings.PreferredFrameworkBranch,
                    PreferredFrameworkTarget = settings.PreferredFrameworkTarget,
                    PreferredFrameworkConfiguration = settings.PreferredFrameworkConfiguration,
                } 
            ).GetAwaiter().GetResult();

            return 0;
        }


    }
}
