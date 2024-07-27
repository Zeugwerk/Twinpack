using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twinpack.Commands
{
    [Verb("update-downloads", HelpText = "Downloads packages that are references in .Zeugwerk/config.json to .Zeugwerk/libraries, you can use RepTool.exe to install them into the TwinCAT library repository.")]
    public class UpdateDownloadsCommand : Command
    {
        [Option('u', "username", Required = false, Default = null, HelpText = "Username for Twinpack Server")]
        public string Username { get; set; }

        [Option('p', "password", Required = false, Default = null, HelpText = "Password for Twinpack Server")]
        public string Password { get; set; }

        [Option('r', "owner", Required = false, Default = "Zeugwerk", HelpText = "")]
        public string RegistryOwner { get; set; }

        [Option('r', "name", Required = false, Default = "Twinpack-Registry", HelpText = "")]
        public string RegistryName { get; set; }

        [Option('d', "dry-run", Required = false, Default = false, HelpText = "")]
        public bool DryRun { get; set; }

        [Option('t', "token", Required = false, Default = null, HelpText = "")]
        public string Token { get; set; }

        public override async Task<int> ExecuteAsync()
        {
            _logger.Info(">>> twinpack-registry:update-downloads");
            var registry = new TwinpackRegistry(_twinpackServer);

            await LoginAsync(Username, Password);
            await registry.UpdateDownloadsAsync(RegistryOwner, RegistryName, token: Token, dryRun: DryRun);

            return 0;
        }
    }
}
