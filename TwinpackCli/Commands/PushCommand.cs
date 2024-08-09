using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Verb("push", HelpText = "Pushes libraries to a Twinpack Server")]
    public class PushCommand : Command
    {
        [Option('u', "username", Required = true, Default = null, HelpText = "Username for Twinpack Server")]
        public string Username { get; set; }

        [Option('p', "password", Required = true, Default = null, HelpText = "Password for Twinpack Server")]
        public string Password { get; set; }

        [Option('t', "configuration", Required = false, Default = "Debug", HelpText = "Package Configuration (Release, Debug, ...)")]
        public string Configuration { get; set; }

        [Option('t', "target", Required = false, Default = "TC3.1", HelpText = "Package Target")]
        public string Target { get; set; }

        [Option('b', "branch", Required = false, Default = "main", HelpText = "Package Branch")]
        public string Branch { get; set; }

        [Option('m', "notes", Required = false, Default = "", HelpText = "Optional release notes, specific to the file that is uploaded")]
        public string Notes { get; set; }

        [Option('C', "compiled", Required = false, Default = false, HelpText = "The package is a compiled-library")]
        public bool Compiled { get; set; }

        [Option('c', "without-config", Required = false, Default = false, HelpText = "Don't use a config.json file, but use the information where to find libraries from the other arguments")]
        public bool WithoutConfig { get; set; }

        [Option('p', "library-path", Required = false, Default = ".", HelpText = "Only valid when without-config is used, path where .library files are located")]
        public string LibraryPath { get; set; }

        [Option('d', "skip-duplicate", Required = false, Default = false, HelpText = "If a package and version already exists, skip it and continue with the next package in the push, if any")]
        public bool SkipDuplicate { get; set; }

        public override int Execute()
        {
            PackagingServerRegistry.InitializeAsync().GetAwaiter().GetResult();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            _twinpack.LoginAsync(Username, Password).GetAwaiter().GetResult();

            foreach (var twinpackServer in PackagingServerRegistry.Servers.Where(x => x as TwinpackServer != null).Select(x => x as TwinpackServer))
            {
                twinpackServer.PushAsync(
                    WithoutConfig ?
                        ConfigPlcProjectFactory.PlcProjectsFromPath(LibraryPath) :
                        ConfigPlcProjectFactory.PlcProjectsFromConfig(Compiled, Target),
                    Configuration,
                    Branch,
                    Target,
                    Notes,
                    Compiled,
                    SkipDuplicate).GetAwaiter().GetResult();
            }

            return 0;
        }
    }
}
