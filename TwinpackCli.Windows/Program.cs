using Spectre.Console.Cli;
using System;
using Twinpack.Commands;

namespace Twinpack
{
    class Program
    {
        [STAThread]
#pragma warning disable CS1998
        static int Main(string[] args)
#pragma warning restore CS1998
        {
            return CliProgram.Run(args, configure =>
            {
                configure.AddCommand<ConfigCommand>("config");
                configure.AddCommand<SearchCommand>("search");
                configure.AddCommand<ResolveCommand>("resolve");
                configure.AddCommand<ListCommand>("list");
                configure.AddCommand<DownloadCommand>("download");
                configure.AddCommand<AddCommand>("add");
                configure.AddCommand<RemoveCommand>("remove");
                configure.AddCommand<RestoreCommand>("restore");
                configure.AddCommand<UpdateCommand>("update");
                configure.AddCommand<SetVersionCommand>("set-version");
                configure.AddCommand<PullCommand>("pull");
                configure.AddCommand<PushCommand>("push");
                configure.Settings.StrictParsing = true;
                configure.Settings.ApplicationName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                configure.Settings.ApplicationVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            });
        }
    }
}
