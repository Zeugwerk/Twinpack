using CommandLine;
using EnvDTE;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Commands;
using Twinpack.Models;
using Twinpack.Protocol;

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
            LogManager.Setup();

            try
            {
                return Parser.Default.ParseArguments<
                    ConfigCommand,
                    SearchCommand, ListCommand,
                    DownloadCommand, 
                    AddCommand, RemoveCommand, 
                    RestoreCommand, UpdateCommand, 
                    SetVersionCommand,
                    PullCommand, PushCommand>(args)
                    .MapResult(
                        (ConfigCommand command) => Execute(command),
                        (SearchCommand command) => Execute(command),
                        (ListCommand command) => Execute(command),
                        (DownloadCommand command) => Execute(command),
                        (AddCommand command) => Execute(command),
                        (RemoveCommand command) => Execute(command),
                        (RestoreCommand command) => Execute(command),
                        (UpdateCommand command) => Execute(command),
                        (SetVersionCommand command) => Execute(command),
                        (PullCommand command) => Execute(command),
                        (PushCommand command) => Execute(command),
                         errs => 1
                    );
            }
            catch(Exception ex)
            {
                _logger.Error(ex.Message);
                _logger.Trace(ex);
                return -1;
            }
            finally
            {
            }
        }

        private static int Execute<T>(T command) where T : Commands.Command
        {
            return command.Execute();
        }
    }
}
