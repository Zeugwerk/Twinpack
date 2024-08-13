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
using Twinpack.Configuration;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace Twinpack.Core
{
    public abstract class AutomationInterface : IAutomationInterface
    {
        public event EventHandler<ProgressEventArgs> ProgressedEvent = delegate { };

        public string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        public string TwincatPath
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Beckhoff\\TwinCAT3\\3.1"))
                    {
                        var bootDir = key?.GetValue("BootDir") as string;

                        // need to do GetParent twice because of the trailing \
                        return bootDir == null ? null : new DirectoryInfo(bootDir)?.Parent?.FullName;
                    }
                }
                catch
                {
                    return null;
                }
            }
        }

        public string LicensesPath { get => TwincatPath + @"\CustomConfig\Licenses"; }
        public string BootFolderPath { get => TwincatPath + @"\Boot"; }

        public bool IsSupported(string tcversion)
        {
            var split = tcversion?.Replace("TC", "").Split('.').Select(x => int.Parse(x)).ToArray();
            var v = new Version(split[0], split[1], split[2], split[3]);
            return (MinVersion == null || v >= MinVersion) && (MaxVersion == null || v <= MaxVersion);
        }

        public abstract string SolutionPath { get; }
        public abstract string ResolveEffectiveVersion(string projectName, string plcName, string placeholderName);
        public abstract Task SetPackageVersionAsync(ConfigPlcProject plc, CancellationToken cancellationToken);
        public abstract Task<bool> IsPackageInstalledAsync(PackageItem package);
        public abstract bool IsPackageInstalled(PackageItem package);
        public abstract Task AddPackageAsync(PackageItem package);
        public abstract Task RemovePackageAsync(PackageItem package, bool uninstall = false, bool forceRemoval = false);
        public abstract Task InstallPackageAsync(PackageItem package, string cachePath = null);
        public abstract Task<bool> UninstallPackageAsync(PackageItem package);
        public abstract Task CloseAllPackageRelatedWindowsAsync(List<PackageItem> packages);
        public abstract void SaveAll();
        protected abstract Version MinVersion { get; }
        protected abstract Version MaxVersion { get; }
    }
}