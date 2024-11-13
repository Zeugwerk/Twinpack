using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using Spectre.Console.Cli;
using Spectre.Console;
using System.Text.Json;

namespace Twinpack.Commands
{
    [Description(@"Lists all packages used in the configuration file located at ./Zeugwerk/config.json, or in the first solution found in the current directory.")]
    public class ListCommand : AbstractCommand<ListCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("-s|--search-term")]
            [Description("Optional search term to filter the listed packages by name or keyword.")]
            public string? SearchTerm { get; set; } = null;

            [CommandOption("--take")]
            [Description("Limits the number of results returned from the search.")]
            public int? Take { get; set; }

            [CommandOption("--plc <ITEM>")]
            [Description("Filters the results to show only packages related to specific PLC(s). By default, all PLCs are considered.")]
            public string[] PlcFilter { get; set; }

            [CommandOption("--outdated")]
            [Description("Displays only the packages that are outdated compared to their latest available versions, considering the configured branch/target/configuration")]
            public bool Outdated { get; set; } = false;
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);
            Initialize(headed: false);

            // remove projects accordingly to the filter
            foreach (var project in _config.Projects)
                project.Plcs = project.Plcs.Where(x => settings.PlcFilter == null || !settings.PlcFilter.Any() || settings.PlcFilter.Contains(x.Name)).ToList();

            var packages = _twinpack.RetrieveUsedPackagesAsync(settings.SearchTerm).GetAwaiter().GetResult()
                .Where(x => !settings.Outdated || x.IsUpdateable);

            if(settings.JsonOutput == true)
            {
                Console.Write(JsonSerializer.Serialize(
                    packages.Select(x => new { 
                        Catalog = x.Catalog, 
                        Package = x.Package,
                        PackageVersion = x.PackageVersion, 
                        Used = x.Used, 
                        Config = x.Config } 
                    )));
            }
            else
            {
                var table = new Table();
                table.AddColumns(new[] { "Project", "Plc", "Package", "Installed Version", "Latest Version", "Updatable" });

                foreach (var package in packages)
                    table.AddRow(new[] { package.ProjectName ?? "n/a", package.PlcName ?? "n/a", package.Catalog?.Name ?? "n/a", package.InstalledVersion ?? "n/a", package.UpdateVersion ?? "n/a", package.IsUpdateable.ToString() ?? "n/a" });

                AnsiConsole.Write(table);
            }


            return 0;
        }
    }
}
