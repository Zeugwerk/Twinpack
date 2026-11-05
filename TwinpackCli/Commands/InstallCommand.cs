using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Twinpack;
using Twinpack.Core;

namespace Twinpack.Commands
{
    [Description("Installs libraries from .Zeugwerk/libraries into the TwinCAT system repository. Opens XAE Shell once on a built-in host solution.")]
    public class InstallCommand : AbstractCommand<InstallCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--library-path")]
            [Description("Folder of downloaded libraries. Defaults to .Zeugwerk/libraries in the working directory.")]
            public string LibraryPath { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

#if !TWINPACK_CLI_FRAMEWORK
            throw new NotSupportedException("Installing libraries requires TwinCAT XAE Shell.");
#else
            var cache = string.IsNullOrWhiteSpace(settings.LibraryPath)
                ? Path.Combine(Environment.CurrentDirectory, ".Zeugwerk", "libraries")
                : Path.GetFullPath(settings.LibraryPath);

            var sw = Stopwatch.StartNew();
            TwinpackRunLog.LogBanner(_logger, "install", "Install cached libraries");

            var packages = LibraryInstallPlan.FromCache(cache);
            _logger.Info("[install] {0} library file(s) in {1}", packages.Count, LogPath.Display(cache));
            LibraryInstallSession.InstallAll(packages, cache);

            TwinpackRunLog.LogPhaseDone(_logger, "install", sw.Elapsed.TotalSeconds);
            return 0;
#endif
        }
    }
}
