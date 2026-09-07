using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Spectre.Console.Cli;
using Twinpack;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description("Pushes libraries to a Twinpack Server")]
    public class PushCommand : AbstractCommand<PushCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("-u|--username")]
            [Description("Username for Twinpack Server")]
            public string Username { get; set; }

            [CommandOption("-p|--password")]
            [Description("Password for Twinpack Server")]
            public string Password { get; set; }

            [CommandOption("--configuration")]
            [Description("Package Configuration (Release, Debug, ...)")]
            public string Configuration { get; set; }

            [CommandOption("--target")]
            [Description("Package Target")]
            public string Target { get; set; }

            [CommandOption("--branch")]
            [Description("Package Branch")]
            public string Branch { get; set; }

            [CommandOption("--notes")]
            [Description("Optional release notes, specific to the file that is uploaded")]
            public string Notes { get; set; }

            [CommandOption("--compiled")]
            [Description("The package is a compiled-library")]
            public bool Compiled { get; set; }

            [CommandOption("--without-config")]
            [Description("Don't use a config.json file, but use the information where to find libraries from the other arguments")]
            public bool WithoutConfig { get; set; }

            [CommandOption("--library-path")]
            [Description("Only valid when without-config is used, path where .library files are located")]
            public string LibraryPath { get; set; }

            [CommandOption("--skip-duplicate")]
            [Description("If a package and version already exists, skip it and continue with the next package in the push, if any")]
            public bool SkipDuplicate { get; set; }

            [CommandOption("--description")]
            [Description("Overrides the package description. Defaults to the library's own Description property.")]
            public string Description { get; set; }

            [CommandOption("--authors")]
            [Description("Overrides the package author(s). Defaults to the library's own Author property.")]
            public string Authors { get; set; }

            [CommandOption("--display-name")]
            [Description("Overrides the human-readable display name shown in the catalog. Defaults to the library's own Title property.")]
            public string DisplayName { get; set; }

            [CommandOption("--project-url")]
            [Description("Link shown in the catalog, e.g. the project's repository or homepage URL.")]
            public string ProjectUrl { get; set; }

            [CommandOption("--entitlement")]
            [Description("Entitlement identifier required to install this package, for gated/commercial packages.")]
            public string Entitlement { get; set; }

            [CommandOption("--license")]
            [Description("License identifier or text shown in the catalog, e.g. 'MIT'.")]
            public string License { get; set; }

            [CommandOption("--license-file")]
            [Description("Path to a license file to upload together with the package.")]
            public string LicenseFile { get; set; }

            [CommandOption("--license-tmc-file")]
            [Description("Path to a .tmc-embedded license file to upload together with the package.")]
            public string LicenseTmcFile { get; set; }

            [CommandOption("--icon-file")]
            [Description("Path to an icon image to upload and show in the catalog.")]
            public string IconFile { get; set; }

            [CommandOption("--binary-download-url")]
            [Description("Records where the binary was originally downloaded from (e.g. an upstream GitHub release asset), for attribution. The binary itself is still uploaded as usual; this is purely informational.")]
            public string BinaryDownloadUrl { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            PackagingServerRegistry.InitializeAsync(useDefaults: true, login: false).GetAwaiter().GetResult();
            _twinpack = new TwinpackService(PackagingServerRegistry.Servers);

            _twinpack.LoginAsync(settings.Username, settings.Password).GetAwaiter().GetResult();

            var plcs = (settings.WithoutConfig ?
                        ConfigPlcProjectFactory.PlcProjectsFromPath(settings.LibraryPath, PackagingServerRegistry.Servers) :
                        ConfigPlcProjectFactory.PlcProjectsFromConfig(settings.Compiled, settings.Target)).ToList();

            if (!plcs.Any())
            {
                throw new Exception("Could not locate any artifacts to push. " 
                    + (settings.WithoutConfig ? $"No .library files in '{settings.LibraryPath}'" : ""));
            }

            // explicit --flags win over whatever was auto-derived from the compiled library for every
            // plc in this push; nothing here is ever persisted back to config.json.
            var overrides = new PlcPublishMetadata
            {
                Description = settings.Description,
                Authors = settings.Authors,
                DisplayName = settings.DisplayName,
                ProjectUrl = settings.ProjectUrl,
                Entitlement = settings.Entitlement,
                License = settings.License,
                LicenseFile = settings.LicenseFile,
                LicenseTmcFile = settings.LicenseTmcFile,
                IconFile = settings.IconFile,
                BinaryDownloadUrl = settings.BinaryDownloadUrl,
            };
            var plcsWithMetadata = plcs.Select(x => (x.Plc, PlcPublishMetadata.Merge(x.Metadata, overrides))).ToList();

            var sw = Stopwatch.StartNew();
            TwinpackRunLog.LogBanner(_logger, "push", "Upload libraries to Twinpack server");

            foreach (var twinpackServer in PackagingServerRegistry.Servers.Where(x => x as TwinpackServer != null).Select(x => x as TwinpackServer))
            {
                _logger.Info("[push] server: {0}", twinpackServer.UrlBase);
                twinpackServer.PushAsync(
                    plcsWithMetadata,
                    settings.Configuration,
                    settings.Branch,
                    settings.Target,
                    settings.Notes,
                    settings.Compiled,
                    settings.SkipDuplicate).GetAwaiter().GetResult();
            }

            TwinpackRunLog.LogPhaseDone(_logger, "push", sw.Elapsed.TotalSeconds);
            return 0;
        }
    }
}
