using CommandLine;
using NLog.Fluent;
using System;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Verb("list", HelpText = @"Searches all package sources defined in the configuration file located at ./Zeugwerk/config.json, or in the first solution found in the current directory.")]
    public class SearchCommand : Command
    {
        [Value(0, MetaName = "search term", Required = false)]
        public string? SearchTerm { get; set; }

        [Option('t', "take", Required = false, Default = null, HelpText = "Limit the number of results to return")]
        public int? Take { get; set; }

        public override int Execute()
        {
            Initialize(headed: false, requiresConfig: false);

            foreach (var package in _twinpack.RetrieveAvailablePackagesAsync(SearchTerm, Take).GetAwaiter().GetResult())
            {
                Console.WriteLine(package.Catalog.Name);
            }

            return 0;
        }
    }
}
