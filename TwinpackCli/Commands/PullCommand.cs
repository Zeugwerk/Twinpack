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
        public class Settings : CommandSettings
        {
            [CommandOption("-u|--username")]
            [Description("Username for Twinpack Server")]
            public string Username { get; set; }

            [CommandOption("-p|--password")]
            [Description("Password for Twinpack Server")]
            public string Password { get; set; }

            [CommandOption("-P|--provided")]
            [Description("Also pull packages that are provided by the package definition")]
            public bool Provided { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            PackagingServerRegistry.InitializeAsync(useDefaults: true, login: false).GetAwaiter().GetResult();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            var config = ConfigFactory.Load();
            _twinpack.LoginAsync(settings.Username, settings.Password).GetAwaiter().GetResult();
            PackagingServerRegistry.Servers.PullAsync(config, skipInternalPackages: !settings.Provided).GetAwaiter().GetResult();
            return 0;
        }
    }
}
