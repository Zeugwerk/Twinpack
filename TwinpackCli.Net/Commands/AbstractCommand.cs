using NLog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;


namespace Twinpack.Commands
{
    public class AbstractSettings : CommandSettings
    {
        [CommandOption("--json-output")]
        [Description("Output data as JSON such that it is machine readable")]
        public bool JsonOutput { get; set; }

        [CommandOption("--verbose")]
        [Description("Verbose console output (only valid without --json-output)")]
        public bool Verbose { get; set; }

        [CommandOption("--quiet")]
        [Description("No console logging (only valid without --verbose)")]
        public bool Quiet { get; set; }
    }

    public abstract class AbstractCommand<TSettings> : Command<TSettings> where TSettings : AbstractSettings
    {
        protected TwinpackService _twinpack;
        protected Config _config;
        protected static Logger _logger;

        public void SetUpLogger(AbstractSettings settings)
        {
            var loggingConfiguration = LogManager.Configuration ?? new NLog.Config.LoggingConfiguration();
            var logFileTraceTarget = new NLog.Targets.FileTarget("Twinpack")
            {
                FileName = @"${specialfolder:folder=LocalApplicationData}\Zeugwerk\logs\Twinpack\TwinpackCli.debug.log",
                MaxArchiveFiles = 7,
                ArchiveEvery = NLog.Targets.FileArchivePeriod.Day,
                ArchiveFileName = @"${specialfolder:folder=LocalApplicationData}\Zeugwerk\logs\Twinpack\TwinpackCli.debug{#}.log",
                ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Rolling,
                KeepFileOpen = false
            };

            if(settings.JsonOutput == false && settings.Quiet == false)
            {
                var logConsoleTarget = new NLog.Targets.ConsoleTarget
                {
                    Layout = "${message} ${onexception:EXCEPTION OCCURRED\\:${exception:format=type,message,method:maxInnerExceptionLevel=5:innerFormat=shortType,message,method}}"
                };

                loggingConfiguration.AddRule(settings.Verbose == true ? LogLevel.Trace : LogLevel.Info, LogLevel.Fatal, logConsoleTarget, "Twinpack.*");
            }

            loggingConfiguration.AddRule(LogLevel.Trace, LogLevel.Fatal, logFileTraceTarget, "Twinpack.*");
            LogManager.Configuration = loggingConfiguration;

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

                // set filepath is null, because we don't want to save this config
                if(_config != null)
                    _config.FilePath = null;
            }

            if (_config == null && requiresConfig)
                throw new FileNotFoundException($@"Configuration file (.\Zeugwerk\config.json) and/or .sln file not found");

#if NET8_0_OR_GREATER
            _twinpack = new TwinpackService(
                PackagingServerRegistry.Servers,
                new AutomationInterfaceHeadless(_config),
                _config);
#else
            _twinpack = new TwinpackService(
                PackagingServerRegistry.Servers,
                headed
                    ? new VisualStudio(hidden: true).Open(_config) 
                    : new AutomationInterfaceHeadless(_config),
                _config);
#endif
        }

        protected List<PackageItem> CreatePackageItems(string[] packages, string projectName = null, string plcName = null)
        {
            return CreatePackageItems(packages, null, null, null, null, projectName, plcName);
        }
        protected List<PackageItem> CreatePackageItems(string[] packages, string[] versions, string[] branches, string[] targets, string[] configurations, string projectName=null, string plcName=null)
        {
            // create temporary configuration, which holds the packages, which should be downloaded
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
