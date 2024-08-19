using CommandLine;
using static Twinpack.Core.TwinpackService;

namespace Twinpack.Commands
{
    [Verb("set-version", HelpText = @"Downloads package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class SetVersionCommand : Command
    {
        [Value(0, MetaName = "version", Required = false)]
        public string Version { get; set; }

        [Option("project", Required = false, HelpText = "Name of the project, which contains the PLC to set the version for. Defaults to null, meaning 'all projects'")]
        public string? ProjectName { get; set; }

        [Option("plc", Required = false, HelpText = "Name of the plc to set the version for. Defaults to null, meaning 'all plcs'")]
        public string? PlcName { get; set; }

        [Option("sync-framework-packages", Required = false, HelpText = "Set the version also for all other packages, which are part of the same framework")]
        public bool SyncFrameworkPackages { get; set; }

        [Option("branch", Required = false, Default = null, HelpText = "Together with 'sync-framework-packages', the preferred branch of framework packages")]
        public string? PreferredFrameworkBranch { get; set; }

        [Option("target", Required = false, Default = null, HelpText = "Together with 'sync-framework-packages', the preferred target of framework packages")]
        public string? PreferredFrameworkTarget { get; set; }

        [Option("configuration", Required = false, Default = null, HelpText = "Together with 'sync-framework-packages', the preferred configuration of framework packages")]
        public string? PreferredFrameworkConfiguration { get; set; }

        [Option("headed", Required = false, Default = false, HelpText = "Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
        public bool Headed { get; set; }
        [Option("purge-packages", Required = false, Default = null, HelpText = "Removes all references that are not configured packages.")]
        public bool PurgePackages { get; set; }
        public override int Execute()
        {
            Initialize(Headed);

            _twinpack.SetPackageVersionAsync(Version, 
                new SetPackageVersionOptions
                {
                    PurgePackages = PurgePackages,
                    ProjectName = ProjectName,
                    PlcName = PlcName,
                    SyncFrameworkPackages = SyncFrameworkPackages,
                    PreferredFrameworkBranch = PreferredFrameworkBranch,
                    PreferredFrameworkTarget = PreferredFrameworkTarget,
                    PreferredFrameworkConfiguration = PreferredFrameworkConfiguration,
                } 
            ).GetAwaiter().GetResult();

            return 0;
        }


    }
}
