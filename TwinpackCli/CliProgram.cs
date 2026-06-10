using NLog;
using Spectre.Console.Cli;
using System;
using System.Linq;
using System.Reflection;
using Twinpack.Commands;

namespace Twinpack
{
    public static class CliProgram
    {
        static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static bool IsHelp(string[] args) =>
            args.Any(arg => arg.Equals("-h", StringComparison.OrdinalIgnoreCase)
                         || arg.Equals("--help", StringComparison.OrdinalIgnoreCase));

        public static int Run(string[] args, Action<IConfigurator> configure)
        {
            TwinpackCliLogging.Initialize();
            TwinpackCliLogging.ConfigureFromArgs(args);

            if (!IsHelp(args))
            {
                _logger.Info(TwinpackRunLog.Separator);
                _logger.Info("twinpack {0}", Assembly.GetExecutingAssembly().GetName().Version);
                _logger.Info(TwinpackRunLog.Separator);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var exitCode = 0;

            var app = new CommandApp();
            app.Configure(configure);

            try
            {
                exitCode = app.Run(args);

                if (!IsHelp(args))
                    TwinpackRunLog.LogResult(_logger, exitCode == 0);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
                _logger.Error(ex.Message);

                if (!IsHelp(args))
                    TwinpackRunLog.LogResult(_logger, false);

                exitCode = -1;
            }
            finally
            {
                watch.Stop();
                if (!IsHelp(args))
                {
                    var ts = watch.Elapsed;
                    _logger.Info("Total time: {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                    _logger.Info("Finished at: {0:dd-MM-yyyy HH:mm:ss}", DateTime.Now);
                    _logger.Info(TwinpackRunLog.Separator);
                }
            }

            return exitCode;
        }
    }
}
