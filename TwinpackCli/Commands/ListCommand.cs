using CommandLine;
using NLog.Fluent;
using System;
using Twinpack.Models;

namespace Twinpack.Commands
{
    [Verb("list", HelpText = @"Displays a list of packages of all sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json. If sourceRepositories.json specifies no sources, uses the\r\n default feeds.")]
    public class ListCommand : Command
    {
        public override int Execute()
        {
            LoginAsync().GetAwaiter().GetResult();
            var it = _packageServers.SearchAsync().GetAsyncEnumerator();
            while(it.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                CatalogItem item = it.Current;
                Console.WriteLine($"{item.Name}");
            }
            return 0;
        }
    }
}
