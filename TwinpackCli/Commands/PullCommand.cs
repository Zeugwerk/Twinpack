using Spectre.Console.Cli;
using System.ComponentModel;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description("Downloads packages that are references in .Zeugwerk/config.json to .Zeugwerk/libraries, you can use RepTool.exe to install them into the TwinCAT library repository.")]
    public class PullCommand : AbstractCommand<PullCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("-P|--provided")]
            [Description("Also pull packages that are provided by the package definition")]
            public bool Provided { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            // Read the saved sources. Credentials come from `twinpack config` (or the
            // Twinpack environment variables), and this command does not write the file.
            PackagingServerRegistry.InitializeAsync(fallbackToDefaults: true, login: false, saveDefaults: false).GetAwaiter().GetResult();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            var config = ConfigFactory.Load();
            _twinpack.LoginAsync().GetAwaiter().GetResult();
            PackagingServerRegistry.Servers.PullAsync(config, skipInternalPackages: !settings.Provided).GetAwaiter().GetResult();
            return 0;
        }
    }
}
