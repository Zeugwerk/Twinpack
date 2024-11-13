using System;
using System.IO;
using System.Linq;
using Twinpack.Protocol;
using Twinpack.Exceptions;
using System.ComponentModel;
using Spectre.Console.Cli;

namespace Twinpack.Commands
{
    [Description(@"Configure or modify package source repositories by updating the settings defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
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

            // load existing configuration
            try
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: false, login: false).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) { }

            // purge if needed
            if (settings.Purge)
            {
                _logger.Info("Purging existing configuration");
                PackagingServerRegistry.PurgeAsync().GetAwaiter().GetResult();
            }

            // reset if needed
            try
            {
                PackagingServerRegistry.InitializeAsync(useDefaults: settings.Reset, login: false).GetAwaiter().GetResult();
            }
            catch (FileNotFoundException) { }

            // add new sources
            var sources = settings.Sources ?? new string[0];
            for (int i = 0; i < sources.Count(); i++)
            {
                var type = settings.Types.ElementAtOrDefault(i) ?? null;
   
                var uri = sources.ElementAt(i);
                _logger.Info($"Adding package server {uri}");
                var packageServer = PackagingServerRegistry.AddServerAsync(
                    type,
                    settings.Names.ElementAtOrDefault(i) ?? uri,
                    uri,
                    login: false).GetAwaiter().GetResult();

                packageServer.Username = settings.Usernames.ElementAtOrDefault(i);
                packageServer.Password = settings.Passwords.ElementAtOrDefault(i);
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
