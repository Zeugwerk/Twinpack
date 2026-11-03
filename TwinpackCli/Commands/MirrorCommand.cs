using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Twinpack;
using Twinpack.Core;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description("Copies packages from one configured package server to another. Beckhoff and NuGet packages are reduced to the library Twinpack installs, then published to the target as a NuGet package or a Twinpack package. Repository files (.tmx, .tml, .tme) are included when the target is NuGet or Beckhoff, and are not uploaded to a Twinpack server.")]
    public class MirrorCommand : AbstractCommand<MirrorCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--source")]
            [Description("Name of the package server to copy from, as configured in sourceRepositories.json.")]
            public string? Source { get; set; }

            [CommandOption("--target")]
            [Description("Name of the package server to copy to, as configured in sourceRepositories.json.")]
            public string? Target { get; set; }

            [CommandOption("--package")]
            [Description("Limits the copy to these package names or feed ids. Repeat for more than one. Omit to copy every package.")]
            public string[] Packages { get; set; }

            [CommandOption("--source-username")]
            [Description("Username for the source server. A blank username with a password is an API key. Stored credentials are used when this is omitted.")]
            public string? SourceUsername { get; set; }

            [CommandOption("--source-password")]
            [Description("Password or API key for the source server. Stored credentials are used when this is omitted.")]
            public string? SourcePassword { get; set; }

            [CommandOption("--target-username")]
            [Description("Username for the target server. A blank username with a password is an API key. Stored credentials are used when this is omitted.")]
            public string? TargetUsername { get; set; }

            [CommandOption("--target-password")]
            [Description("Password or API key for the target server. Stored credentials are used when this is omitted.")]
            public string? TargetPassword { get; set; }

            public override ValidationResult Validate()
            {
                var baseResult = base.Validate();
                if (!baseResult.Successful)
                    return baseResult;

                if (string.IsNullOrWhiteSpace(Source))
                    return ValidationResult.Error("The source package server name is required (--source).");

                if (string.IsNullOrWhiteSpace(Target))
                    return ValidationResult.Error("The target package server name is required (--target).");

                return ValidationResult.Success();
            }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            PackagingServerRegistry.InitializeAsync(useDefaults: false, login: false).GetAwaiter().GetResult();
            var source = PackageMirror.Resolve(settings.Source ?? "");
            var target = PackageMirror.Resolve(settings.Target ?? "");

            source.LoginAsync(settings.SourceUsername, settings.SourcePassword).GetAwaiter().GetResult();
            target.LoginAsync(settings.TargetUsername, settings.TargetPassword).GetAwaiter().GetResult();

            var sw = Stopwatch.StartNew();
            TwinpackRunLog.LogBanner(_logger, "mirror", "Copy PLC libraries from one server to another");
            _logger.Info("[mirror] {0} ({1}) -> {2} ({3})", source.Name, source.ServerType, target.Name, target.ServerType);

            if (settings.Packages != null && settings.Packages.Length > 0)
                _logger.Info("[mirror] packages: {0}", string.Join(", ", settings.Packages));

            var result = PackageMirror.RunAsync(source, target, settings.Packages).GetAwaiter().GetResult();
            _logger.Info("[mirror] copied {0}, skipped {1}, failed {2}", result.Copied, result.Skipped, result.Failed);

            if (settings.UseJsonOutput)
            {
                CliJson.Write(new
                {
                    copied = result.Copied,
                    skipped = result.Skipped,
                    failed = result.Failed,
                    failures = result.Failures
                });
            }

            var failed = result.Failed > 0;
            TwinpackRunLog.LogPhaseDone(_logger, "mirror", sw.Elapsed.TotalSeconds, failed);
            return failed ? 1 : 0;
        }
    }
}
