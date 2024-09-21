﻿using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;


namespace Twinpack.Commands
{
    public abstract class AbstractCommand<TSettings> : Spectre.Console.Cli.Command<TSettings> where TSettings : Spectre.Console.Cli.CommandSettings
    {
        protected TwinpackService _twinpack;
        protected Config _config;

        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

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

            _twinpack = new TwinpackService(
                PackagingServerRegistry.Servers,
                headed
                    ? new VisualStudio(hidden: true).Open(_config) 
                    : new AutomationInterfaceHeadless(_config),
                _config);
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