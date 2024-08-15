using CommandLine;
using NLog.Fluent;
using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;
using NuGet.Packaging;
using NuGet.Protocol.Plugins;
using System.Net.PeerToPeer;
using System.Diagnostics;
using Twinpack.Exceptions;

namespace Twinpack.Commands
{
    [Verb("config", HelpText = @"Configure or modify package source repositories by updating the settings defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class ConfigCommand : Command
    {
        [Option("type", Required = false, HelpText = "Specifies the type(s) of package server(s), i.e. 'Twinpack Repository', 'NuGet Repository', 'Beckhoff Repository'")]
        public IEnumerable<string> Types { get; set; }
        [Option("source", Required = false, HelpText = "Specifies the URL(s) of the package server(s) from which packages will be handled.")]
        public IEnumerable<string> Sources { get; set; }
        [Option("name", Required = false, HelpText = "Specifies the name(s) for the package server(s). Defaults to the corresponding source URL if not provided.")]
        public IEnumerable<string> Names { get; set; }
        [Option("username", Required = false, HelpText = "Specifies the username(s) required to authenticate with the package server(s), if applicable.")]
        public IEnumerable<string> Usernames { get; set; }
        [Option("password", Required = false, HelpText = "Specifies the password(s) required to authenticate with the package server(s), if applicable.")]
        public IEnumerable<string> Passwords { get; set; }
        [Option("purge", Required = false, HelpText = "Deletes all currently configured package servers, clearing the configuration file and deleting stored credentials")]
        public bool Purge { get; set; }
        [Option("reset", Required = false, HelpText = "Resets the configuration by purging all currently configured package servers and restoring default settings.")]
        public bool Reset { get; set; }
        public override int Execute()
        {
            // load existing configuration
            try
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: false, login: false).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) { }

            // purge if needed
            if (Purge)
            {
                _logger.Info("Purging existing configuration");
                PackagingServerRegistry.PurgeAsync().GetAwaiter().GetResult();
            }

            // reset if needed
            try
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: Reset, login: false).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) { }

            // add new sources
            for (int i = 0; i < Sources.Count(); i++)
            {
                var type = Types.ElementAtOrDefault(i) ?? null;
   
                var uri = Sources.ElementAt(i);
                _logger.Info($"Adding package server {uri}");
                var packageServer = PackagingServerRegistry.AddServerAsync(
                    type,
                    Names.ElementAtOrDefault(i) ?? uri,
                    uri,
                    login: false).GetAwaiter().GetResult();

                packageServer.Username = Usernames.ElementAtOrDefault(i);
                packageServer.Password = Passwords.ElementAtOrDefault(i);
            }

            // try to login
            foreach (var packageServer in PackagingServerRegistry.Servers)
            {
                try
                {
                    packageServer.LoginAsync(packageServer.Username, packageServer.Password).GetAwaiter().GetResult();
                }
                catch (LoginException ex)
                {
                    _logger.Warn($"Log in to '{packageServer.UrlBase}' failed");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);
                    _logger.Trace(ex);
                }
            }

            PackagingServerRegistry.Save();

            return 0;
        }
    }
}
