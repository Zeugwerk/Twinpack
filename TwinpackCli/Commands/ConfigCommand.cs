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
    [Verb("config", HelpText = @"Restore package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class ConfigCommand : Command
    {
        [Option("type", Required = false, HelpText = "Url(s) of the package server(s)")]
        public IEnumerable<string> Types { get; set; }
        [Option("source", Required = false, HelpText = "Url(s) of the package server(s)")]
        public IEnumerable<string> Sources { get; set; }
        [Option("name", Required = false, HelpText = "Name(s) of the package server(s), defaults to the source name")]
        public IEnumerable<string> Names { get; set; }
        [Option("username", Required = false, HelpText = "Username(s) of the package server(s)")]
        public IEnumerable<string> Usernames { get; set; }
        [Option("password", Required = false, HelpText = "Password(s) of the package server(s)")]
        public IEnumerable<string> Passwords { get; set; }
        [Option("purge", Required = false, HelpText = "Purge all servers that are currently configured")]
        public bool Purge { get; set; }
        [Option("reset", Required = false, HelpText = "Purge all servers that are currently configured")]
        public bool Reset { get; set; }
        public override int Execute()
        {
            try
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: Reset, login: false).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) { }

            if (Purge)
            {
                _logger.Info("Purging existing configuration");
                PackagingServerRegistry.PurgeAsync().GetAwaiter().GetResult();
            }

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
