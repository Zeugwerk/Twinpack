using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;

namespace Twinpack.Commands
{
    public static class TwinpackCliLogging
    {
        const string ConsoleTargetName = "TwinpackConsole";
        const string FileTargetName = "TwinpackFile";

        public static void Initialize()
        {
            var nlogConfig = Path.Combine(AppContext.BaseDirectory, "NLog.dll.nlog");
            if (File.Exists(nlogConfig))
                LogManager.Setup().LoadConfigurationFromFile(nlogConfig);

            EnsureFileTarget();
        }

        static void EnsureFileTarget()
        {
            var config = LogManager.Configuration ?? new LoggingConfiguration();

            if (config.FindTargetByName(FileTargetName) == null)
            {
                config.AddTarget(new FileTarget(FileTargetName)
                {
                    FileName = @"${specialfolder:folder=LocalApplicationData}/Zeugwerk/logs/Twinpack/TwinpackCli.debug.log",
                    MaxArchiveFiles = 7,
                    ArchiveEvery = FileArchivePeriod.Day,
                    ArchiveFileName = @"${specialfolder:folder=LocalApplicationData}/Zeugwerk/logs/Twinpack/TwinpackCli.debug{#}.log",
                    ArchiveNumbering = ArchiveNumberingMode.Rolling,
                    KeepFileOpen = false,
                    Layout = "${longdate} ${uppercase:${level}} ${logger} ${message} ${exception:format=ToString}"
                });
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, FileTargetName, "Twinpack.*");
            }

            LogManager.Configuration = config;
            LogManager.ReconfigExistingLoggers();
        }

        public static void ConfigureForCommand(AbstractSettings settings)
        {
            var config = LogManager.Configuration ?? new LoggingConfiguration();
            EnsureFileTarget();

            var consoleTarget = config.FindTargetByName(ConsoleTargetName) as Target
                ?? config.FindTargetByName("ConsoleLogger");

            if (consoleTarget != null)
            {
                for (int i = config.LoggingRules.Count - 1; i >= 0; i--)
                {
                    var rule = config.LoggingRules[i];
                    if (rule.Targets.Contains(consoleTarget))
                        config.LoggingRules.RemoveAt(i);
                }
            }
            else
            {
                consoleTarget = new ColoredConsoleTarget(ConsoleTargetName)
                {
                    UseDefaultRowHighlightingRules = false,
                    EnableAnsiOutput = true,
                    DetectOutputRedirected = true,
                    Layout = "${message} ${onexception:EXCEPTION OCCURRED\\:${exception:format=type,message,method:maxInnerExceptionLevel=5:innerFormat=shortType,message,method}}"
                };
                config.AddTarget(consoleTarget);
            }

            if (!settings.JsonOutput && !settings.Quiet)
            {
                var minLevel = settings.Verbose ? LogLevel.Trace : LogLevel.Info;
                config.AddRule(minLevel, LogLevel.Fatal, consoleTarget, "Twinpack.*");
            }

            LogManager.Configuration = config;
            LogManager.ReconfigExistingLoggers();
        }
    }
}
