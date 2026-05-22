using NLog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Twinpack;
using Twinpack;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    public class AbstractSettings : CommandSettings
    {
        [CommandOption("-o|--output <FORMAT>")]
        [Description("Console output format: text (default, human-readable) or json (machine-readable on stdout; log messages use stderr).")]
        public string? Output { get; set; }

        [CommandOption("--json")]
        [Description("Shorthand for --output json.")]
        public bool Json { get; set; }

        [CommandOption("--verbose")]
        [Description("Verbose console logging (stderr when using json output).")]
        public bool Verbose { get; set; }

        [CommandOption("--quiet")]
        [Description("No console logging (log file is still written).")]
        public bool Quiet { get; set; }

        public bool UseJsonOutput =>
            Json
            || string.Equals(Output?.Trim(), "json", StringComparison.OrdinalIgnoreCase);

        public override ValidationResult Validate()
        {
            var format = Output?.Trim();
            if (!string.IsNullOrEmpty(format)
                && !string.Equals(format, "text", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Error("Output format must be 'text' or 'json'.");
            }

            if (Json && string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Error("Cannot combine --json with --output text.");

            return ValidationResult.Success();
        }
    }

    public abstract class AbstractCommand<TSettings> : Command<TSettings> where TSettings : AbstractSettings
    {
        protected TwinpackService _twinpack;
        protected Config _config;
        protected static Logger _logger;

#if TWINPACK_CLI_FRAMEWORK
        /// <summary>CLI-spawned XAE automation host; disposed after each command when <c>--headed</c>.</summary>
        protected VisualStudio _ownedVisualStudio;
#endif

        public void SetUpLogger(AbstractSettings settings)
        {
            TwinpackCliLogging.ConfigureForCommand(settings);
            _logger = LogManager.GetCurrentClassLogger();
        }

        protected void Initialize(bool headed, bool requiresConfig = true)
        {
            PackagingServerRegistry.InitializeAsync(useDefaults: false).GetAwaiter().GetResult();
            var rootPath = Environment.CurrentDirectory;

            _config = ConfigFactory.Load(rootPath);

            if (_config == null)
            {
                _config = ConfigFactory.CreateFromSolutionFileAsync(
                    rootPath,
                    continueWithoutSolution: false,
                    packageServers: PackagingServerRegistry.Servers.Where(x => x.Connected),
                    plcTypeFilter: null).GetAwaiter().GetResult();

                if(_config != null)
                    _config.FilePath = null;
            }

            if (_config != null)
            {
                if (!string.IsNullOrEmpty(_config.FilePath))
                    _logger.Info("[config] using: {0}", LogPath.Display(_config.FilePath));
                else if (!string.IsNullOrEmpty(_config.Solution))
                    _logger.Info("[config] solution: {0}", LogPath.Display(_config.Solution));
            }

            if (_config == null && requiresConfig)
                throw new FileNotFoundException($@"Configuration file (.\Zeugwerk\config.json) and/or .sln file not found");

#if TWINPACK_CLI_FRAMEWORK
            _ownedVisualStudio = null;
            if (headed)
            {
                _ownedVisualStudio = new VisualStudio(hidden: true);
                try
                {
                    var automation = _ownedVisualStudio.Open(_config);
                    _twinpack = new TwinpackService(PackagingServerRegistry.Servers, automation, _config);
                }
                catch
                {
                    DisposeOwnedVisualStudio();
                    throw;
                }
            }
            else
            {
                _twinpack = new TwinpackService(
                    PackagingServerRegistry.Servers,
                    new AutomationInterfaceHeadless(_config),
                    _config);
            }
#else
            _twinpack = new TwinpackService(
                PackagingServerRegistry.Servers,
                new AutomationInterfaceHeadless(_config),
                _config);
#endif
        }

#if TWINPACK_CLI_FRAMEWORK
        protected void DisposeOwnedVisualStudio()
        {
            if (_ownedVisualStudio == null)
                return;
            try
            {
                _ownedVisualStudio.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, "Failed to shut down TwinCAT XAE automation host");
            }
            finally
            {
                _ownedVisualStudio = null;
            }
        }
#endif

        protected int RunWithAutomationTeardown(Func<int> action)
        {
#if TWINPACK_CLI_FRAMEWORK
            try
            {
                return action();
            }
            finally
            {
                DisposeOwnedVisualStudio();
            }
#else
            return action();
#endif
        }

        protected List<PackageItem> CreatePackageItems(string[] packages, string projectName = null, string plcName = null)
        {
            return CreatePackageItems(packages, null, null, null, null, projectName, plcName);
        }
        protected List<PackageItem> CreatePackageItems(string[] packages, string[] versions, string[] branches, string[] targets, string[] configurations, string projectName=null, string plcName=null)
        {
            List<PackageItem> packageItems = new List<PackageItem>();
            for (int i = 0; i < (packages?.Count() ?? 0); i++)
            {
                packageItems.Add(new PackageItem
                {
                    ProjectName = projectName,
                    PlcName = plcName,
                    Config = new ConfigPlcPackage
                    {
                        Name = packages?.ElementAt(i),
                        Version = versions?.ElementAtOrDefault(i),
                        Branch = branches?.ElementAtOrDefault(i),
                        Target = targets?.ElementAtOrDefault(i),
                        Configuration = configurations?.ElementAtOrDefault(i)
                    }
                });
            }

            return packageItems;
        }

    }
}
