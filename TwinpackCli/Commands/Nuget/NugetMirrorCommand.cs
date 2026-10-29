#nullable disable
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    [Description("Mirrors packages with the given tag(s) from a Beckhoff tcpkg feed into a Sonatype Nexus hosted NuGet repository.")]
    public class NugetMirrorCommand : AbstractCommand<NugetMirrorCommand.Settings>
    {
        public class Settings : AbstractSettings
        {
            [CommandOption("--feed-url")]
            [Description("Beckhoff feed root. Defaults to the public stable tcpkg feed.")]
            public string FeedUrl { get; set; }

            [CommandOption("--feed-username")]
            [Description("Username for the Beckhoff feed. Leave blank when the password is an API key. Used for this run only.")]
            public string FeedUsername { get; set; }

            [CommandOption("--feed-password")]
            [Description("Password or API key for the Beckhoff feed. Used for this run only.")]
            public string FeedPassword { get; set; }

            [CommandOption("--tag")]
            [Description("Tag to mirror. Repeat for more than one. A package is kept when its tags contain any of these as a whole word.")]
            public string[] Tags { get; set; }

            [CommandOption("--latest-only")]
            [Description("Keep only the newest version of each package after the tag filter.")]
            public bool LatestOnly { get; set; }

            [CommandOption("--nexus-url")]
            [Description("Nexus hosted NuGet repository root, for example https://nexus.example/repository/nuget-hosted/")]
            public string NexusUrl { get; set; }

            [CommandOption("--nexus-username")]
            [Description("Username for the Nexus repository. Leave blank when the password is a NuGet API key. Used for this run only.")]
            public string NexusUsername { get; set; }

            [CommandOption("--nexus-password")]
            [Description("Password or NuGet API key for the Nexus repository. Used for this run only.")]
            public string NexusPassword { get; set; }

            [CommandOption("--dry-run")]
            [Description("List the packages that would be downloaded and pushed. Does not write to Nexus.")]
            public bool DryRun { get; set; }

            public override ValidationResult Validate()
            {
                var inherited = base.Validate();
                if (!inherited.Successful)
                    return inherited;

                if (Tags == null || !Tags.Any(tag => !string.IsNullOrWhiteSpace(tag)))
                    return ValidationResult.Error("At least one --tag is required.");

                if (string.IsNullOrWhiteSpace(NexusUrl))
                    return ValidationResult.Error("--nexus-url is required.");

                return ValidationResult.Success();
            }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            SetUpLogger(settings);

            var sw = Stopwatch.StartNew();
            TwinpackRunLog.LogBanner(_logger, "mirror", "Mirror Beckhoff packages into Nexus");

            var feedUrl = string.IsNullOrWhiteSpace(settings.FeedUrl) ? BeckhoffServer.DefaultUrlBase : settings.FeedUrl;
            var source = new BeckhoffServer("beckhoff", feedUrl);
            source.LoginAsync(settings.FeedUsername, settings.FeedPassword, persistCredentials: false).GetAwaiter().GetResult();

            var result = NugetFeedMirror.RunAsync(
                source,
                settings.NexusUrl,
                settings.NexusUsername,
                settings.NexusPassword,
                settings.Tags,
                settings.LatestOnly,
                settings.DryRun).GetAwaiter().GetResult();

            if (settings.UseJsonOutput)
                CliJson.Write(result);
            else if (settings.DryRun)
                _logger.Info("[mirror] would push {0}, skipped {1}, failed {2}", result.Pushed.Count, result.Skipped.Count, result.Failed.Count);
            else
                _logger.Info("[mirror] pushed {0}, skipped {1}, failed {2}", result.Pushed.Count, result.Skipped.Count, result.Failed.Count);

            TwinpackRunLog.LogPhaseDone(_logger, "mirror", sw.Elapsed.TotalSeconds, failed: result.Failed.Count > 0);
            return result.Failed.Count == 0 ? 0 : 1;
        }
    }
}
