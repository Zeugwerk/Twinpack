using NLog;
using Twinpack.Commands;
using Spectre.Console.Cli;

namespace Twinpack
{
    class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        [STAThread]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        static int Main(string[] args)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            _logger.Info("sadf");
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.AddCommand<ConfigCommand>("config");
                config.AddCommand<SearchCommand>("search");
                config.AddCommand<ListCommand>("list");
                config.AddCommand<DownloadCommand>("download");
                config.AddCommand<AddCommand>("add");
                config.AddCommand<RemoveCommand>("remove");
                config.AddCommand<RestoreCommand>("restore");
                config.AddCommand<UpdateCommand>("update");
                config.AddCommand<SetVersionCommand>("set-version");
                config.AddCommand<PullCommand>("pull");
                config.AddCommand<PushCommand>("push");
                config.Settings.StrictParsing = true;
                config.Settings.ApplicationName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                config.Settings.ApplicationVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            });

            try
            {
                return app.Run(args);
            }
            catch(Exception ex)
            {
                _logger.Error(ex.Message);
                _logger.Error(ex);
                return -1;
            }
            finally
            {
            }
        }
    }
}
