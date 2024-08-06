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

        [Option("headless", Required = false, Default = false, HelpText = "Do not use the Automation Interface, some actions are not available with this option. For instance, using the headless mode will always download packages even if they would already exist on the system, because the Automation Interface can not be used to check if they already exist or not")]
        public bool Headless { get; set; }

        public override int Execute()
        {
            if (Headless)
                throw new NotImplementedException("Headless is not implemented atm!");

            Initialize(Headless);

            var packages = CreatePackageItems(Packages, ProjectName, PlcName);

            _twinpack.RemovePackagesAsync(packages, uninstall: false).GetAwaiter().GetResult();

            return 0;
        }
    }
}
