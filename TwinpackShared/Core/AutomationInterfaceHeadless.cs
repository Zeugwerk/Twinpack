using EnvDTE;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Models;
using Twinpack.Models.Api;

namespace Twinpack.Core
{
    public class AutomationInterfaceHeadless : AutomationInterface, IAutomationInterface
    {
        public event EventHandler<ProgressEventArgs> ProgressedEvent = delegate { };

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private Config _config;

        public AutomationInterfaceHeadless(Config config)
        {
            _config = config;
        }

        protected override Version MinVersion => new Version(3, 1, 4024, 0);
        protected override Version MaxVersion => null;

        public override string SolutionPath { get => Path.GetDirectoryName(_config.FilePath); }

        public override void SaveAll()
        {
            ;
        }

        public override string ResolveEffectiveVersion(string projectName, string plcName, string placeholderName)
        {
            return null;
        }

        public override async Task<bool> IsPackageInstalledAsync(PackageItem package)
        {
            return true;
        }

        public override bool IsPackageInstalled(PackageItem package)
        {
            return true;
        }

        public override async Task CloseAllPackageRelatedWindowsAsync(List<PackageItem> packages)
        {
            
        }

        public override async Task AddPackageAsync(PackageItem package)
        {
            throw new NotImplementedException();
        }

        public override async Task RemovePackageAsync(PackageItem package, bool uninstall=false)
        {
            throw new NotImplementedException();
        }

        public override async Task InstallPackageAsync(PackageItem package, string cachePath = null)
        {
            throw new NotImplementedException("Headless AutomationInterface can not install packages");
        }

        public override async Task<bool> UninstallPackageAsync(PackageItem package)
        {
            throw new NotImplementedException("Headless AutomationInterface can not uninstall packages");
        }
    }
}