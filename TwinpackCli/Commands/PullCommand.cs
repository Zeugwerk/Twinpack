﻿using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Verb("pull", HelpText = "Downloads packages that are references in .Zeugwerk/config.json to .Zeugwerk/libraries, you can use RepTool.exe to install them into the TwinCAT library repository.")]
    public class PullCommand : Command
    {
        [Option('u', "username", Required = false, Default = null, HelpText = "Username for Twinpack Server")]
        public string Username { get; set; }

        [Option('p', "password", Required = false, Default = null, HelpText = "Password for Twinpack Server")]
        public string Password { get; set; }

        [Option('P', "provided", Required = false, Default = false, HelpText = "Also pull packages that are provided by the package definition")]
        public bool Provided { get; set; }

        public override async Task<int> ExecuteAsync()
        {
            await PackagingServerRegistry.InitializeAsync();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            var config = ConfigFactory.Load();
            await _twinpack.LoginAsync(Username, Password);
            await PackagingServerRegistry.Servers.PullAsync(config, skipInternalPackages: !Provided);
            return 0;
        }
    }
}
