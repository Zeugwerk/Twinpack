#nullable enable
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using Twinpack.Core;

namespace Twinpack.Commands
{
    [Description(@"Resolves a specific package version from the sources defined in '.\sourceRepositories.json' or '%APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json'. Exits with code 0 if the version is found, 1 if it is not.")]
    public class ResolveCommand : AbstractCommand<ResolveCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--package")]
            [Description("Name of the package to resolve.")]
            public string? Package { get; set; }

            [CommandOption("--version")]
            [Description("Preferred version of the package. If omitted, the server resolves the latest available version.")]
            public string? Version { get; set; }

            [CommandOption("--branch")]
            [Description("Preferred branch of the package (e.g. 'main').")]
            public string? Branch { get; set; }

            [CommandOption("--target")]
            [Description("Preferred target of the package (e.g. 'TC3.1').")]
            public string? Target { get; set; }

            [CommandOption("--configuration")]
            [Description("Preferred configuration of the package (e.g. 'Release').")]
            public string? Configuration { get; set; }

            [CommandOption("--strict")]
            [Description("Fail (exit code 1) if the resolved package does not exactly match every explicitly provided option (--version, --branch, --target, --configuration). Useful for CI checks that verify a specific build was uploaded.")]
            public bool Strict { get; set; }

            public override ValidationResult Validate()
            {
                if (string.IsNullOrWhiteSpace(Package))
                    return ValidationResult.Error("--package is required.");

                return base.Validate();
            }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);
            Initialize(headed: false, requiresConfig: false);

            return RunWithAutomationTeardown(() =>
            {
                var resolved = _twinpack.ResolvePackageAsync(
                    settings.Package,
                    new TwinpackService.ResolvePackageOptions
                    {
                        PreferredVersion = settings.Version,
                        PreferredBranch = settings.Branch,
                        PreferredTarget = settings.Target,
                        PreferredConfiguration = settings.Configuration,
                    }).GetAwaiter().GetResult();

                var found = !string.IsNullOrEmpty(resolved?.Name);

                var mismatches = new List<string>();
                if (found && settings.Strict)
                {
                    if (settings.Version != null && !string.Equals(resolved!.Version, settings.Version, StringComparison.OrdinalIgnoreCase))
                        mismatches.Add($"version: requested '{settings.Version}', got '{resolved.Version}'");
                    if (settings.Branch != null && !string.Equals(resolved!.Branch, settings.Branch, StringComparison.OrdinalIgnoreCase))
                        mismatches.Add($"branch: requested '{settings.Branch}', got '{resolved.Branch}'");
                    if (settings.Target != null && !string.Equals(resolved!.Target, settings.Target, StringComparison.OrdinalIgnoreCase))
                        mismatches.Add($"target: requested '{settings.Target}', got '{resolved.Target}'");
                    if (settings.Configuration != null && !string.Equals(resolved!.Configuration, settings.Configuration, StringComparison.OrdinalIgnoreCase))
                        mismatches.Add($"configuration: requested '{settings.Configuration}', got '{resolved.Configuration}'");
                }

                var success = found && mismatches.Count == 0;

                if (settings.UseJsonOutput)
                {
                    Console.Write(JsonSerializer.Serialize(resolved));
                }
                else
                {
                    if (found)
                    {
                        var table = new Table();
                        table.AddColumns("Package", "Version", "Branch", "Target", "Configuration");
                        table.AddRow(
                            resolved!.Name ?? string.Empty,
                            resolved.Version ?? string.Empty,
                            resolved.Branch ?? string.Empty,
                            resolved.Target ?? string.Empty,
                            resolved.Configuration ?? string.Empty);
                        AnsiConsole.Write(table);
                    }
                    else
                    {
                        _logger.Warn("Package '{0}' could not be resolved.", settings.Package);
                    }

                    foreach (var mismatch in mismatches)
                        _logger.Error("[resolve] strict mismatch -- {0}", mismatch);
                }

                return success ? 0 : 1;
            });
        }
    }
}
