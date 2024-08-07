using CommandLine;
using System;
using System.Collections.Generic;
using Twinpack.Core;

namespace Twinpack.Commands
{
    [Verb("remove", HelpText = @"Downloads package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class RemoveCommand : Command
    {
        [Option("project", Required = true, HelpText = "Name of the project, where packages should be added")]
        public string ProjectName { get; set; }

        [Option("plc", Required = true, HelpText = "Name of the plc, where packages should be added")]
        public string PlcName { get; set; }

        [Option("package", Required = false, HelpText = "Package(s) to handle")]
        public IEnumerable<string> Packages { get; set; }

        [Option("headed", Required = false, Default = false, HelpText = "Use Beckhoff Automation Interface, some actions are not available without this argument")]
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
