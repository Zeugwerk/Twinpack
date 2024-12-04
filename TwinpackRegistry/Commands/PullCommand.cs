using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Verb("pull", HelpText = "Downloads packages that are references in .Zeugwerk/config.json to .Zeugwerk/libraries, you can use RepTool.exe to install them into the TwinCAT library repository.")]
    public class PullCommand : Command
    {
        [Option('u', "username", Required = false, Default = null, HelpText = "Username for Twinpack Repository")]
        public string Username { get; set; }

        [Option('p', "password", Required = false, Default = null, HelpText = "Password for Twinpack Repository")]
        public string Password { get; set; }

        [Option("beckhoff-username", Required = false, Default = null, HelpText = "Username for Beckhoff Repository")]
        public string BeckhoffUsername { get; set; }

        [Option("beckhoff-password", Required = false, Default = null, HelpText = "Password for Beckhoff Repository")]
        public string BeckhoffPassword { get; set; }

        [Option('r', "owner", Required = false, Default = "Zeugwerk", HelpText = "")]
        public string RegistryOwner { get; set; }

        [Option('r', "name", Required = false, Default = "Twinpack-Registry", HelpText = "")]
        public string RegistryName { get; set; }

        [Option('d', "dry-run", Required = false, Default = false, HelpText = "")]
        public bool DryRun { get; set; }

        [Option('D', "dump", Required = false, Default = false, HelpText = "")]
        public bool Dump { get; set; }

        [Option('t', "token", Required = false, Default = false, HelpText = "")]
        public string Token { get; set; }

        public override async Task<int> ExecuteAsync()
        {
            _logger.Info(">>> twinpack-registry:pull");

            var registry = new TwinpackRegistry(new List<IPackageServer> { _twinpackServer, _beckhoffServer });
            await LoginAsync(Username, Password, BeckhoffUsername, BeckhoffPassword);


            _logger.Info(new string('-', 3) + $" download");
            await registry.DownloadAsync(RegistryOwner, RegistryName, token: Token);

            var plcs = ConfigPlcProjectFactory.PlcProjectsFromConfig(compiled: false, target: "TC3.1");
            if (!DryRun && plcs.Any())
            {
                _logger.Info(new string('-', 3) + $" push");
                await _twinpackServer.PushAsync(plcs, "Release", "main", "TC3.1", null, false);
            }

            return 0;
        }
    }
}
