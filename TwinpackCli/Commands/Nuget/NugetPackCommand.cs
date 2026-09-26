using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Spectre.Console.Cli;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description("Packs built libraries into NuGet packages (.nupkg), one per library. This writes files only, it does not upload anything - push the packages to your NuGet server with 'nuget push' or your pipeline's publish step. To publish to a Twinpack repository instead, use 'twinpack push'.")]
    public class NugetPackCommand : AbstractCommand<NugetPackCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--target")]
            [Description("TwinCAT target the libraries were built for, e.g. TC3.1. Selects which build output is packed and is also the folder the library is placed in inside the package.")]
            public string Target { get; set; } = "TC3.1";

            [CommandOption("--compiled")]
            [Description("Pack .compiled-library files instead of .library files. Adds the 'tp-compiled-library' tag Twinpack needs to recognize them.")]
            public bool Compiled { get; set; }

            [CommandOption("--output-directory")]
            [Description("Directory the .nupkg files are written to. Defaults to .Zeugwerk/nupkg.")]
            public string OutputDirectory { get; set; }

            [CommandOption("--without-config")]
            [Description("Don't use a config.json file, but pack every .library found in --library-path")]
            public bool WithoutConfig { get; set; }

            [CommandOption("--library-path")]
            [Description("Only valid together with --without-config, path where the .library files are located")]
            public string LibraryPath { get; set; }

            [CommandOption("--description")]
            [Description("Overrides the package description. Defaults to the library's own Description property.")]
            public string Description { get; set; }

            [CommandOption("--authors")]
            [Description("Overrides the package author(s), which Twinpack reads back as the distributor name. Defaults to the PLC's Company property.")]
            public string Authors { get; set; }

            [CommandOption("--display-name")]
            [Description("Overrides the package title. Defaults to the library's own Title property.")]
            public string DisplayName { get; set; }

            [CommandOption("--project-url")]
            [Description("Link shown in the catalog, e.g. the project's repository or homepage URL.")]
            public string ProjectUrl { get; set; }

            [CommandOption("--license")]
            [Description("SPDX license expression, e.g. 'MIT'. Identifiers that are not valid SPDX are skipped with a warning, because NuGet rejects them.")]
            public string License { get; set; }

            [CommandOption("--license-file")]
            [Description("Path to a license file to embed in the package. Takes precedence over --license.")]
            public string LicenseFile { get; set; }

            [CommandOption("--icon-file")]
            [Description("Path to an icon image to embed in the package and show in the catalog.")]
            public string IconFile { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            var sw = Stopwatch.StartNew();
            TwinpackRunLog.LogBanner(_logger, "pack", "Pack libraries into NuGet packages");

            // Packing is offline, so unlike `push` there is no login to perform. --without-config
            // still needs the package servers to resolve the dependencies it reads from the binary.
            List<(ConfigPlcProject Plc, PlcPublishMetadata Metadata)> plcs;
            if (settings.WithoutConfig)
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: true, login: false).GetAwaiter().GetResult();
                plcs = ConfigPlcProjectFactory.PlcProjectsFromPath(settings.LibraryPath, PackagingServerRegistry.Servers).ToList();
            }
            else
            {
                plcs = ConfigPlcProjectFactory.PlcProjectsFromConfig(settings.Compiled, settings.Target).ToList();
            }

            if (!plcs.Any())
            {
                throw new Exception("Could not locate any artifacts to pack. "
                    + (settings.WithoutConfig ? $"No .library files in '{settings.LibraryPath}'" : ""));
            }

            // explicit --flags win over whatever was derived from the compiled library, same
            // precedence as `twinpack push`.
            var overrides = new PlcPublishMetadata
            {
                Description = settings.Description,
                Authors = settings.Authors,
                DisplayName = settings.DisplayName,
                ProjectUrl = settings.ProjectUrl,
                License = settings.License,
                LicenseFile = settings.LicenseFile,
                IconFile = settings.IconFile,
            };

            var outputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
                ? NugetPackService.DefaultOutputPath
                : settings.OutputDirectory;

            var packages = new List<string>();
            foreach (var (plc, metadata) in plcs)
            {
                packages.Add(NugetPackService.PackLibrary(
                    plc,
                    plc.FilePath,
                    settings.Target,
                    settings.Compiled,
                    outputDirectory,
                    PlcPublishMetadata.Merge(metadata, overrides)));
            }

            if (settings.UseJsonOutput)
                Console.Write(JsonSerializer.Serialize(packages));

            TwinpackRunLog.LogPhaseDone(_logger, "pack", sw.Elapsed.TotalSeconds);
            return 0;
        }
    }
}
