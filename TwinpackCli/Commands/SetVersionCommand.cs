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

        [Option("headed", Required = false, Default = false, HelpText = "Use Beckhoff Automation Interface, some actions are not available without this argument")]
        public bool Headed { get; set; }
        public override int Execute()
        {
            Initialize(Headed);

            _twinpack.SetPackageVersionAsync(Version, 
                new SetPackageVersionOptions
                {
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
