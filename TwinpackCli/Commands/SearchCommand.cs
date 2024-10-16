using NLog.Fluent;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description(@"Searches all package sources defined in the configuration file located at ./Zeugwerk/config.json, or in the first solution found in the current directory.")]
    public class SearchCommand : AbstractCommand<SearchCommand.Settings>
    {
        public class Settings : Twinpack.Commands.AbstractSettings
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
            SetUpLogger(settings);
            Initialize(headed: false, requiresConfig: false);

            var packages = _twinpack.RetrieveAvailablePackagesAsync(settings.SearchTerm, settings.Take).GetAwaiter().GetResult()
                .Where(x => x.Catalog != null)
                .Select(x => x.Catalog);

            if (settings.JsonOutput == true)
            {
                Console.Write(JsonSerializer.Serialize(packages));
            }
            else
            {
                var table = new Table();
                table.AddColumns(new[] { "Package", "Distributor" });

                foreach (var package in packages)
                    table.AddRow(new[] { package.Name, package.DistributorName });

                AnsiConsole.Write(table);
            }


            return 0;
        }
    }
}
