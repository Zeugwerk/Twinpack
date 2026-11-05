using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Twinpack;
using Twinpack.Core;

namespace Twinpack.Commands
{
    [Description(@"Downloads package(s) from the sources defined in '.\sourceRepositories.json' or '%APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json'.")]
    public class DownloadCommand : AbstractCommand<DownloadCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--package")]
            [Description("Package(s) to download. When set, no configuration file or solution is required, and the packages are downloaded even if they are already installed. When omitted, packages come from .Zeugwerk/config.json or the solution in the working directory.")]
            public string[] Packages { get; set; }
            [CommandOption("--version")]
            [Description("Specifies the version(s) of the package(s) to be downloaded. If not explicitly set, Twinpack downloads the latest version.")]
            public string[] Versions { get; set; }
            [CommandOption("--branch")]
            [Description("Specifies the branch(es) of the package(s). If not provided, Twinpack automatically determines the appropriate branch.")]
            public string[] Branches { get; set; }
            [CommandOption("--target")]
            [Description("Specifies the target(s) for the package(s). If omitted, Twinpack will automatically select the appropriate target.")]
            public string[] Targets { get; set; }
            [CommandOption("--configuration")]
            [Description("Specifies the configuration(s) of the package(s). If not set, Twinpack will automatically select the appropriate configuration.")]
            public string[] Configurations { get; set; }
            [CommandOption("--force-download")]
            [Description("Forces the download of package(s) even if they are already available on the system. Implied when --package is set.")]
            public bool ForceDownload { get; set; }
            [CommandOption("--include-provided-packages")]
            [Description("Includes the download of packages that are provided by the configuration (e.g., PLCs that are also considered packages by being available on a configured package server). Packages named with --package are always included.")]
            public bool IncludeProvidedPackages { get; set; }
            [CommandOption("--headed")]
            [Description("Opens XAE Shell and skips libraries that are already installed. Used only when --package is omitted. Defaults to false")]
            public bool Headed { get; set; }
            [CommandOption("--install")]
            [Description("After the download, installs the libraries fetched by this run into the TwinCAT system repository. One XAE Shell is opened for the whole set.")]
            public bool Install { get; set; }
        }
        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            // Named packages are fetched as given. The installed-library check is the only reason
            // download opens XAE during the fetch, and it needs a solution, so both stay on the
            // configuration path. --install opens one shell after the fetch, for the whole set.
            var explicitPackages = settings.Packages != null;

#if !TWINPACK_CLI_FRAMEWORK
            if (settings.Install)
                throw new NotSupportedException("Installing libraries requires TwinCAT XAE Shell.");
#endif

            if (!explicitPackages && !settings.ForceDownload && !settings.Headed && !settings.Install)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(headed: explicitPackages || settings.Install ? false : settings.Headed, requiresConfig: !explicitPackages);

            return RunWithAutomationTeardown(() =>
            {
                var sw = Stopwatch.StartNew();
                TwinpackRunLog.LogBanner(_logger, "download", "Download packages to local cache");

                var packages = CreatePackageItems(settings.Packages, settings.Versions, settings.Branches, settings.Targets, settings.Configurations);

                if (settings.Packages == null)
                    packages = _twinpack.RetrieveUsedPackagesAsync().GetAwaiter().GetResult().ToList();

                _logger.Info("[download] {0} package(s) to process", packages.Count);

                var downloaded = _twinpack.DownloadPackagesAsync(packages,
                    new TwinpackService.DownloadPackageOptions
                    {
                        IncludeProvidedPackages = settings.IncludeProvidedPackages,
                        ProvidedPackagesToDownload = settings.Packages?.ToList(),
                        ForceDownload = explicitPackages || settings.ForceDownload,
                    }).GetAwaiter().GetResult();

                _logger.Info("[download] {0} package(s) written to cache", downloaded.Count);

                if (settings.Install)
                {
#if TWINPACK_CLI_FRAMEWORK
                    var cache = Path.Combine(Environment.CurrentDirectory, ".Zeugwerk", "libraries");
                    var batch = LibraryInstallPlan.FromDownloaded(downloaded, cache);
                    LibraryInstallSession.InstallAll(batch, cache);
#endif
                }

                TwinpackRunLog.LogPhaseDone(_logger, "download", sw.Elapsed.TotalSeconds);
                return 0;
            });
        }
    }
}
