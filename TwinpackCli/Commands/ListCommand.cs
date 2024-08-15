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

namespace Twinpack.Commands
{
    [Verb("list", HelpText = @"Lists all packages used in the configuration file located at ./Zeugwerk/config.json, or in the first solution found in the current directory.")]
    public class ListCommand : Command
    {
        [Value(0, MetaName = "search term", Required = false, HelpText = "Optional search term to filter the listed packages by name or keyword.")]
        public string? SearchTerm { get; set; }

        [Option("take", Required = false, Default = null, HelpText = "Limits the number of results returned from the search.")]
        public int? Take { get; set; }

        [Option("plc", Required = false, Default = null, HelpText = "Filters the results to show only packages related to specific PLC(s). By default, all PLCs are considered.")]
        public IEnumerable<string> PlcFilter { get; set; }

        [Option("outdated", Required = false, Default = null, HelpText = "Displays only the packages that are outdated compared to their latest available versions, considering the configured branch/target/configuration")]
        public bool Outdated { get; set; }

        public override int Execute()
        {
            Initialize(headed: false);

            // remove projects accordingly to the filter
            foreach (var project in _config.Projects)
                project.Plcs = project.Plcs.Where(x => !PlcFilter.Any() || PlcFilter.Contains(x.Name)).ToList();

            var packages = _twinpack.RetrieveUsedPackagesAsync(SearchTerm).GetAwaiter().GetResult();
            foreach (var package in packages.Where(x => !Outdated || x.IsUpdateable))
                Console.WriteLine($"{package.ProjectName}:{package.PlcName}: {package.Catalog?.Name} {package.InstalledVersion} {(package.IsUpdateable ? $"-> {package.UpdateVersion}" : "")}");

            return 0;
        }
    }
}
