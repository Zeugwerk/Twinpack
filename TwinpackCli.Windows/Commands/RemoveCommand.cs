using Spectre.Console.Cli;
using System.Collections.Generic;
using System.ComponentModel;

namespace Twinpack.Commands
{
    [Description(@"Removes package(s) from the specified project and PLC using the sources defined in '.\sourceRepositories.json' or '%APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json'.")]
    public class RemoveCommand : AbstractCommand<RemoveCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--project")]
            [Description("The name of the project where the packages should be added.")]
            public string ProjectName { get; set; }

            [CommandOption("--plc <PLC>")]
            [Description("The name of the PLC where the packages will be added.")]
            public string PlcName { get; set; }

            [CommandOption("--package")]
            [Description("Specifies the package(s) to be added.")]
            public string[] Packages { get; set; }

            [CommandOption("--headed")]
            [Description("Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
            public bool Headed { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            Initialize(settings.Headed);

            var packages = CreatePackageItems(settings.Packages, settings.ProjectName, settings.PlcName);

            _twinpack.RemovePackagesAsync(packages, uninstall: false).GetAwaiter().GetResult();

            return 0;
        }
    }
}
