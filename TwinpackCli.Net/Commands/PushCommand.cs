using Spectre.Console.Cli;
using System.ComponentModel;
using System.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description("Pushes libraries to a Twinpack Server")]
    public class PushCommand : AbstractCommand<PushCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("-u|--username")]
            [Description("Username for Twinpack Server")]
            public string Username { get; set; }

            [CommandOption("-p|--password")]
            [Description("Password for Twinpack Server")]
            public string Password { get; set; }

            [CommandOption("--configuration")]
            [Description("Package Configuration (Release, Debug, ...)")]
            public string Configuration { get; set; }

            [CommandOption("--target")]
            [Description("Package Target")]
            public string Target { get; set; }

            [CommandOption("--branch")]
            [Description("Package Branch")]
            public string Branch { get; set; }

            [CommandOption("--notes")]
            [Description("Optional release notes, specific to the file that is uploaded")]
            public string Notes { get; set; }

            [CommandOption("--compiled")]
            [Description("The package is a compiled-library")]
            public bool Compiled { get; set; }

            [CommandOption("--without-config")]
            [Description("Don't use a config.json file, but use the information where to find libraries from the other arguments")]
            public bool WithoutConfig { get; set; }

            [CommandOption("--library-path")]
            [Description("Only valid when without-config is used, path where .library files are located")]
            public string LibraryPath { get; set; }

            [CommandOption("--skip-duplicate")]
            [Description("If a package and version already exists, skip it and continue with the next package in the push, if any")]
            public bool SkipDuplicate { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            PackagingServerRegistry.InitializeAsync(useDefaults: true, login: false).GetAwaiter().GetResult();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            _twinpack.LoginAsync(settings.Username, settings.Password).GetAwaiter().GetResult();

            foreach (var twinpackServer in PackagingServerRegistry.Servers.Where(x => x as TwinpackServer != null).Select(x => x as TwinpackServer))
            {
                twinpackServer.PushAsync(
                    settings.WithoutConfig ?
                        ConfigPlcProjectFactory.PlcProjectsFromPath(settings.LibraryPath, PackagingServerRegistry.Servers) :
                        ConfigPlcProjectFactory.PlcProjectsFromConfig(settings.Compiled, settings.Target),
                    settings.Configuration,
                    settings.Branch,
                    settings.Target,
                    settings.Notes,
                    settings.Compiled,
                    settings.SkipDuplicate).GetAwaiter().GetResult();
            }

            return 0;
        }
    }
}
