using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Spectre.Console.Cli;
using Twinpack;
using Twinpack.Exceptions;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description(@"Configure or modify package source repositories by updating the settings defined in '.\sourceRepositories.json' or '%APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json'.")]
    public class ConfigCommand : AbstractCommand<ConfigCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--type")]
            [Description("Specifies the type(s) of package server(s), i.e. 'Twinpack Repository', 'NuGet Repository', 'Beckhoff Repository'")]
            public string[] Types { get; set; }
            [CommandOption("--source")]
            [Description("Specifies the URL(s) of the package server(s) from which packages will be handled.")]
            public string[] Sources { get; set; }
            [CommandOption("--name")]
            [Description("Specifies the name(s) for the package server(s). Defaults to the corresponding source URL if not provided.")]
            public string[] Names { get; set; }
            [CommandOption("--username")]
            [Description("Specifies the username(s) required to authenticate with the package server(s), if applicable.")]
            public string[] Usernames { get; set; }
            [CommandOption("--password")]
            [Description("Specifies the password(s) required to authenticate with the package server(s), if applicable.")]
            public string[] Passwords { get; set; }
            [CommandOption("--purge")]
            [Description("Deletes all currently configured package servers, clearing the configuration file and deleting stored credentials")]
            public bool Purge { get; set; }
            [CommandOption("--reset")]
            [Description("Resets the configuration by purging all currently configured package servers and restoring default settings.")]
            public bool Reset { get; set; }

        }
        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            var sw = Stopwatch.StartNew();
            TwinpackRunLog.LogBanner(_logger, "config", "Configure package source repositories");

            try
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: false, login: false).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) { }

            if (settings.Purge)
            {
                _logger.Info("[config] purging existing configuration");
                PackagingServerRegistry.PurgeAsync().GetAwaiter().GetResult();
            }

            try
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: settings.Reset, login: false).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) { }

            var sources = settings.Sources ?? new string[0];
            for (int i = 0; i < sources.Count(); i++)
            {
                var type = settings.Types?.ElementAtOrDefault(i) ?? null;
   
                var uri = sources.ElementAt(i);
                _logger.Info("[config] adding server: {0}", uri);
                var packageServer = PackagingServerRegistry.AddServerAsync(
                    type,
                    settings.Names?.ElementAtOrDefault(i) ?? uri,
                    uri,
                    login: false).GetAwaiter().GetResult();

                packageServer.Username = settings.Usernames?.ElementAtOrDefault(i);
                packageServer.Password = settings.Passwords?.ElementAtOrDefault(i);
            }

            foreach (var packageServer in PackagingServerRegistry.Servers)
            {
                try
                {
                    packageServer.LoginAsync(packageServer.Username, packageServer.Password).GetAwaiter().GetResult();
                }
                catch (LoginException)
                {
                    _logger.Warn("[config] log in failed: {0}", packageServer.UrlBase);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);
                    _logger.Trace(ex);
                }
            }

            PackagingServerRegistry.Save();

            if (settings.UseJsonOutput)
            {
                Console.Write(JsonSerializer.Serialize(PackagingServerRegistry.Servers));
            }

            TwinpackRunLog.LogPhaseDone(_logger, "config", sw.Elapsed.TotalSeconds);
            return 0;
        }
    }
}
