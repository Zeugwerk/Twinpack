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
    [Verb("list", HelpText = @"Searches all uses packages sources defined ./Zeugwerk/config.json or in the first solution found in the current directory")]
    public class ListCommand : Command
    {
        [Value(0, MetaName = "search term", Required = false)]
        public string? SearchTerm { get; set; }

        [Option("take", Required = false, Default = null, HelpText = "Limit the number of results to return")]
        public int? Take { get; set; }

        [Option("plc", Required = false, Default = null, HelpText = "Filter for specific plcs, by default all plcs are considered")]
        public IEnumerable<string> PlcFilter { get; set; }

        [Option("outdated", Required = false, Default = null, HelpText = "Only show outdated packages")]
        public bool Outdated { get; set; }

        public override async Task<int> ExecuteAsync()
        {
            await PackagingServerRegistry.InitializeAsync();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            await _twinpack.LoginAsync();

            var rootPath = Environment.CurrentDirectory;
            var config = ConfigFactory.Load(rootPath);

            if (config == null)
            {
                config = await ConfigFactory.CreateFromSolutionFileAsync(
                    rootPath,
                    continueWithoutSolution: false,
                    packageServers: PackagingServerRegistry.Servers.Where(x => x.Connected),
                    plcTypeFilter: null);
            }

            foreach (var project in config.Projects)
            {
                project.Plcs = project.Plcs.Where(x => !PlcFilter.Any() || PlcFilter.Contains(x.Name)).ToList();
            }

            var packages = await _twinpack.RetrieveInstalledPackagesAsync(config, SearchTerm);
            foreach (var package in packages.Where(x => !Outdated || x.IsUpdateable))
            {
                Console.WriteLine($"{package.Name} {package.InstalledVersion} {(package.IsUpdateable ? $"-> {package.UpdateVersion}" : "")}");
            }

            return 0;
        }
    }
}
