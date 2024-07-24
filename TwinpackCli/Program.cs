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
        static int Main(string[] args)
        {
            LogManager.Setup();

            try
            {
                return Parser.Default.ParseArguments<PullCommand, PushCommand>(args)
                    .MapResult(
                    (PullCommand command) => Execute(command),
                    (PushCommand command) => Execute(command),
                    errs => 1);
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

        private static int Execute<T>(T command) where T : Commands.Command
        {
            return command.Execute();
        }
    }
}
