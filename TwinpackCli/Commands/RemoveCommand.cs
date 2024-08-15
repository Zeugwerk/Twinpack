using CommandLine;
using System;
using System.Collections.Generic;
using Twinpack.Core;

namespace Twinpack.Commands
{
    [Verb("remove", HelpText = @"Removes package(s) from the specified project and PLC using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class RemoveCommand : Command
    {
        [Option("project", Required = true, HelpText = "The name of the project from which the packages should be removed.")]
        public string ProjectName { get; set; }

        [Option("plc", Required = true, HelpText = "The name of the PLC from which the packages should be removed.")]
        public string PlcName { get; set; }

        [Option("package", Required = false, HelpText = "The package(s) to be removed. If not specified, all packages related to the project and PLC will be considered.")]
        public IEnumerable<string> Packages { get; set; }

        [Option("headed", Required = false, Default = false, HelpText = "Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
        public bool Headed { get; set; }

        public override int Execute()
        {
            Initialize(Headed);

            var packages = CreatePackageItems(Packages, ProjectName, PlcName);

            _twinpack.RemovePackagesAsync(packages, uninstall: false).GetAwaiter().GetResult();

            return 0;
        }
    }
}
