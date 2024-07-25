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
        static async Task<int> Main(string[] args)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            LogManager.Setup();

            try
            {
                return Parser.Default.ParseArguments<SearchCommand, PullCommand, PushCommand>(args)
                    .MapResult(
                        (SearchCommand command) => ExecuteAsync(command).GetAwaiter().GetResult(),
                        (PullCommand command) => ExecuteAsync(command).GetAwaiter().GetResult(),
                        (PushCommand command) => ExecuteAsync(command).GetAwaiter().GetResult(),
                         errs => 1
                    );
            }
            catch(Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
            finally
            {
            }
        }

        private static async Task<int> ExecuteAsync<T>(T command) where T : Commands.Command
        {
            return await command.ExecuteAsync();
        }
    }
}
