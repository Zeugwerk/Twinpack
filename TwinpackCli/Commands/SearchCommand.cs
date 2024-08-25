using NLog.Fluent;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description(@"Searches all package sources defined in the configuration file located at ./Zeugwerk/config.json, or in the first solution found in the current directory.")]
    public class SearchCommand : AbstractCommand<SearchCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandOption("-s|--search-term")]
            [Description("Optional search term to filter the listed packages by name or keyword.")]
            public string? SearchTerm { get; set; } = null;

            [CommandOption("--take")]
            [Description("Limit the number of results to return")]
            public int? Take { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            Initialize(headed: false, requiresConfig: false);

            var table = new Table();
            table.AddColumns(new[] { "Package", "Distributor" });

            foreach (var package in _twinpack.RetrieveAvailablePackagesAsync(settings.SearchTerm, settings.Take).GetAwaiter().GetResult())
                table.AddRow(new[] { package.Catalog?.Name, package.Catalog?.DistributorName });

            AnsiConsole.Write(table);

            return 0;
        }
    }
}
