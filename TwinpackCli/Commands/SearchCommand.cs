using CommandLine;
using NLog.Fluent;
using System;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Verb("search", HelpText = @"Searches all given sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json using the query string provided")]
    public class SearchCommand : Command
    {
        [Value(0, MetaName = "search term", Required = false)]
        public string? SearchTerm { get; set; }

        [Option('t', "take", Required = false, Default = null, HelpText = "Limit the number of results to return")]
        public int? Take { get; set; }

        public override int Execute()
        {
            PackagingServerRegistry.InitializeAsync().GetAwaiter().GetResult();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            _twinpack.LoginAsync().GetAwaiter().GetResult();
            foreach (var package in _twinpack.RetrieveAvailablePackagesAsync(SearchTerm, Take).GetAwaiter().GetResult())
            {
                Console.WriteLine(package.Name);
            }

            return 0;
        }
    }
}
