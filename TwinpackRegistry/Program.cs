using CommandLine;
using NLog;
using System;
using System.Threading.Tasks;
using Twinpack.Commands;
using Twinpack.Protocol;

namespace Twinpack
{
    class Program
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();


        [STAThread]
        static int Main(string[] args)
        {
            _logger.Info("-------------------------------------------------------------------------");
            _logger.Info($"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            _logger.Info("-------------------------------------------------------------------------");

            LogManager.Setup();
            var watch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                return Parser.Default.ParseArguments<PullCommand, UpdateDownloadsCommand, DumpCommand>(args)
                    .MapResult(
                    (PullCommand command) => ExecuteAsync(command).GetAwaiter().GetResult(),
                    (UpdateDownloadsCommand command) => ExecuteAsync(command).GetAwaiter().GetResult(),
                    (DumpCommand command) => ExecuteAsync(command).GetAwaiter().GetResult(),
                    errs => 1);
            }
            catch (Exception ex)
            {
               _logger.Error(ex);

                _logger.Info("-------------------------------------------------------------------------");
                _logger.Info("FAILED");
                _logger.Info("-------------------------------------------------------------------------");

                throw;
            }
            finally
            {
                PackagingServerRegistry.PurgeAsync().GetAwaiter().GetResult();
                watch.Stop();
                TimeSpan ts = watch.Elapsed;
                string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

                _logger.Info($"Total time: {elapsedTime}");
                _logger.Info($"Finished at: {DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")}");
                _logger.Info("-------------------------------------------------------------------------");
            }
        }

        private static async Task<int> ExecuteAsync<T>(T command) where T : Commands.Command
        {
            var exitCode = await command.ExecuteAsync();

            if (exitCode == 0)
            {
                _logger.Info("-------------------------------------------------------------------------");
                _logger.Info("SUCCESS");
                _logger.Info("-------------------------------------------------------------------------");
            }

            return exitCode;
        }
    }
}
