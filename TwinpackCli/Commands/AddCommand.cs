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
using System.Xml.Linq;

namespace Twinpack.Commands
{
    [Verb("add", HelpText = @"Adds package(s) to the specified project and PLC by utilizing the sources listed in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json. " +
                         "Ensure that the configuration file is properly set up using 'twinpack.exe config' before executing this command.")]
    public class AddCommand : Command
    {
        [Option("project", Required = true, HelpText = "The name of the project where the packages should be added.")]
        public string ProjectName { get; set; }

        [Option("plc", Required = true, HelpText =" The name of the PLC where the packages will be added.")]
        public string PlcName { get; set; }

        [Option("package", Required = false, HelpText = "Specifies the package(s) to be added.")]
        public IEnumerable<string> Packages { get; set; }

        [Option("version", Required = false, Default = null, HelpText = "Defines the version(s) of the specified package(s). If omitted, Twinpack will automatically add the latest available version.")]
        public IEnumerable<string> Versions { get; set; }

        [Option("branch", Required = false, Default = null, HelpText = "Specifies the branch(es) of the package(s). If not provided, Twinpack will determine the appropriate branch automatically, i.e. 'main'")]
        public IEnumerable<string> Branches { get; set; }

        [Option("target", Required = false, Default = null, HelpText = "Indicates the target(s) for the package(s). If omitted, Twinpack will select the appropriate target(s) automatically, i.e. 'TC3.1'")]
        public IEnumerable<string> Targets { get; set; }

        [Option("configuration", Required = false, Default = null, HelpText = "Specifies the configuration(s) for the package(s). If not specified, Twinpack will handle the configuration automatically, i.e. 'Release'")]
        public IEnumerable<string> Configurations { get; set; }

        [Option("add-dependencies", Required = false, Default = true, HelpText = "If true, package dependencies will be added automatically to the PLC references.")]
        public bool AddDependencies { get; set; }

        [Option("force-download", Required = false, Default = null, HelpText = "Forces the download of package(s) even if they are already available on the system.")]
        public bool ForceDownload { get; set; }

        [Option("headed", Required = false, Default = false, HelpText = "Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
        public bool Headed { get; set; }

        public override int Execute()
        {
            Initialize(Headed);

            var packages = CreatePackageItems(Packages, Versions, Branches, Targets, Configurations, ProjectName, PlcName);

            _twinpack.AddPackagesAsync(packages, new TwinpackService.AddPackageOptions { ForceDownload=ForceDownload, IncludeDependencies=AddDependencies }).GetAwaiter().GetResult();

            return 0;
        }
    }
}
