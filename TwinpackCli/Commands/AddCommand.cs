using System.Collections.Generic;
using Twinpack.Core;
using System.ComponentModel;
using Spectre.Console.Cli;

namespace Twinpack.Commands
{
    [Description("Ensure that the configuration file is properly set up using 'twinpack.exe config' before executing this command.")]
    public class AddCommand : AbstractCommand<AddCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandOption("--project <PROJECT>")]
            [Description("The name of the project where the packages should be added.")]
            public string ProjectName { get; set; }

            [CommandOption("--plc <PLC>")]
            [Description("The name of the PLC where the packages will be added.")]
            public string PlcName { get; set; }

            [CommandOption("--package")]
            [Description("Specifies the package(s) to be added.")]
            public string[] Packages { get; set; }

            [CommandOption("--version")]
            [Description("Defines the version(s) of the specified package(s). If omitted, Twinpack will automatically add the latest available version.")]
            public string[] Versions { get; set; }

            [CommandOption("--branch")]
            [Description("Specifies the branch(es) of the package(s). If not provided, Twinpack will determine the appropriate branch automatically, i.e. 'main'")]
            public string[] Branches { get; set; }

            [CommandOption("--target")]
            [Description("Indicates the target(s) for the package(s). If omitted, Twinpack will select the appropriate target(s) automatically, i.e. 'TC3.1'")]
            public string[] Targets { get; set; }

            [CommandOption("--configuration")]
            [Description("Specifies the configuration(s) for the package(s). If not specified, Twinpack will handle the configuration automatically, i.e. 'Release'")]
            public string[] Configurations { get; set; }

            [CommandOption("--add-dependencies")]
            [Description("If true, package dependencies will be added automatically to the PLC references.")]
            public bool AddDependencies { get; set; }

            [CommandOption("--force-download")]
            [Description("Forces the download of package(s) even if they are already available on the system.")]
            public bool ForceDownload { get; set; }

            [CommandOption("--headed")]
            [Description("Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
            public bool Headed { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            Initialize(settings.Headed);

            var packages = CreatePackageItems(settings.Packages, settings.Versions, settings.Branches, settings.Targets, settings.Configurations, settings.ProjectName, settings.PlcName);
            _twinpack.AddPackagesAsync(packages, new TwinpackService.AddPackageOptions { ForceDownload=settings.ForceDownload, IncludeDependencies= settings.AddDependencies }).GetAwaiter().GetResult();
            return 0;
        }
    }
}
