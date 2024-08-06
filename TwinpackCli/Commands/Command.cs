using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;


namespace Twinpack.Commands
{
    public abstract class Command
    {
        protected TwinpackService _twinpack;
        protected Config _config;

        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public abstract int Execute();

        protected void Initialize(bool headless)
        {
            PackagingServerRegistry.InitializeAsync().GetAwaiter().GetResult();
            var rootPath = Environment.CurrentDirectory;

            _config = ConfigFactory.Load(rootPath);

            if (_config == null)
            {
                _config = ConfigFactory.CreateFromSolutionFileAsync(
                    rootPath,
                    continueWithoutSolution: false,
                    packageServers: PackagingServerRegistry.Servers.Where(x => x.Connected),
                    plcTypeFilter: null).GetAwaiter().GetResult();
            }

            PackagingServerRegistry.InitializeAsync().GetAwaiter().GetResult();

            _twinpack = new TwinpackService(
                PackagingServerRegistry.Servers,
                headless 
                    ? null 
                    : new VisualStudio(hidden: true).Open(_config),
                _config);

            _twinpack.LoginAsync().GetAwaiter().GetResult();
        }

        protected List<PackageItem> CreatePackageItems(IEnumerable<string> packages, IEnumerable<string> versions, IEnumerable<string> branches, IEnumerable<string> targets, IEnumerable<string> configurations, string projectName=null, string plcName=null)
        {
            // create temporary configuration, which holds the packages, which should be downloaded
            List<PackageItem> packageItems = new List<PackageItem>();
            for (int i = 0; i < packages.Count(); i++)
            {
                packageItems.Add(new PackageItem
                {
                    ProjectName = projectName,
                    PlcName = plcName,
                    Config = new ConfigPlcPackage
                    {
                        Name = packages.ElementAt(i),
                        Version = versions.ElementAtOrDefault(i) ?? null,
                        Branch = branches.ElementAtOrDefault(i) ?? null,
                        Target = targets.ElementAtOrDefault(i) ?? null,
                        Configuration = configurations.ElementAtOrDefault(i) ?? null
                    }
                });
            }

            return packageItems;
        }

    }
}
