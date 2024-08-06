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
    [Verb("add", HelpText = @"Downloads package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class AddCommand : Command
    {
        [Option("project", Required = true, HelpText = "Name of the project, where packages should be added")]
        public string ProjectName { get; set; }

        [Option("plc", Required = true, HelpText = "Name of the plc, where packages should be added")]
        public string PlcName { get; set; }

        [Option("package", Required = false, HelpText = "Package(s) to handle")]
        public IEnumerable<string> Packages { get; set; }

        [Option("version", Required = false, Default = null, HelpText = "Version(s) of the handled package(s), if not explicitly set, Twinpack downloads the latest version")]
        public IEnumerable<string> Versions { get; set; }

        [Option("branch", Required = false, Default = null, HelpText = "Branches(s) of the handled package(s), if not explicitly set, Twinpack looks up the apporiate option automatically")]
        public IEnumerable<string> Branches { get; set; }

        [Option("target", Required = false, Default = null, HelpText = "Target(s) of the handled package(s), if not explicitly set, Twinpack looks up the apporiate option automatically")]
        public IEnumerable<string> Targets { get; set; }

        [Option("configuration", Required = false, Default = null, HelpText = "Configuration(s) of the handled package(s), if not explicitly set, Twinpack looks up the apporiate option automatically")]
        public IEnumerable<string> Configurations { get; set; }

        [Option("add-dependencies", Required = false, Default = true, HelpText = "Add package dependencies as well to the references")]
        public bool AddDependencies { get; set; }

        [Option("force-download", Required = false, Default = null, HelpText = "Download packages even if they are already available on the system")]
        public bool ForceDownload { get; set; }

        [Option("headless", Required = false, Default = false, HelpText = "Do not use the Automation Interface, some actions are not available with this option. For instance, using the headless mode will always download packages even if they would already exist on the system, because the Automation Interface can not be used to check if they already exist or not")]
        public bool Headless { get; set; }

        public override int Execute()
        {
            if (Headless)
                throw new NotImplementedException("Headless is not implemented atm!");

            Initialize(Headless);

            var packages = CreatePackageItems(Packages, Versions, Branches, Targets, Configurations, ProjectName, PlcName);

            _twinpack.AddPackagesAsync(packages, new TwinpackService.AddPackageOptions { ForceDownload=ForceDownload, AddDependencies=AddDependencies }).GetAwaiter().GetResult();

            return 0;
        }
    }
}
