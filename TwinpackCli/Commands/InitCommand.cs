using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Spectre.Console.Cli;
using Twinpack.Core;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description("Creates '.Zeugwerk/config.json' for the solution in the current directory if it doesn't exist yet. Never overwrites an existing file.")]
    public class InitCommand : AbstractCommand<InitCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            var sw = Stopwatch.StartNew();
            TwinpackRunLog.LogBanner(_logger, "init", "Ensure .Zeugwerk/config.json exists");

            PackagingServerRegistry.InitializeAsync(useDefaults: false).GetAwaiter().GetResult();

            var result = TwinpackService.EnsureConfigFileAsync(
                Environment.CurrentDirectory,
                PackagingServerRegistry.Servers.Where(x => x.Connected)).GetAwaiter().GetResult();

            if (settings.UseJsonOutput)
                Console.Write(JsonSerializer.Serialize(new { path = result.Path, created = result.Created }));

            TwinpackRunLog.LogPhaseDone(_logger, "init", sw.Elapsed.TotalSeconds);
            return 0;
        }
    }
}
