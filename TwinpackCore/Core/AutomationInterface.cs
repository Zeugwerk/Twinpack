using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Models;

#if !NETSTANDARD2_1_OR_GREATER
using Microsoft.Win32;
#endif

namespace Twinpack.Core
{
    public abstract class AutomationInterface : IAutomationInterface
    {
        public event EventHandler<EventArgs> ProgressedEvent = delegate { };

        protected virtual void OnProgressedEvent()
        {
            ProgressedEvent?.Invoke(this, new EventArgs());
        }

        public string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        public string? TwincatPath
        {
            get
            {
                try
                {
#if NETSTANDARD2_1_OR_GREATER
                    return null;
#else
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Beckhoff\\TwinCAT3\\3.1"))
                    {
                        var bootDir = key?.GetValue("BootDir") as string;

                        // need to do GetParent twice because of the trailing \
                        return bootDir == null ? null : new DirectoryInfo(bootDir)?.Parent?.FullName;
                    }
#endif
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

        public static string NormalizedVersion(string version)
        {
            version = version?.Trim().TrimStart(new char[] { 'v', 'V', ' ', '\t' }).Replace('-', '.');
            if (version != null && !Version.TryParse(version, out _))
                throw new ArgumentException("Version has wrong format! Valid formats include '1.0.0.0', 'v1.0.0.0', '1.0.0-0'");

            return version;
        }

        public abstract string SolutionPath { get; }
        public abstract Task<string> ResolveEffectiveVersionAsync(string projectName, string plcName, string placeholderName);
        public abstract Task SetPackageVersionAsync(ConfigPlcProject plc, CancellationToken cancellationToken = default);
        public abstract Task<bool> IsPackageInstalledAsync(PackageItem package);
        public abstract bool IsPackageInstalled(PackageItem package);
        public abstract Task AddPackageAsync(PackageItem package);
        public abstract Task RemovePackageAsync(PackageItem package, bool uninstall = false, bool forceRemoval = false);
        public abstract Task RemoveAllPackagesAsync(string projectName, string plcName);
        public abstract Task InstallPackageAsync(PackageItem package, string cachePath = null);
        public abstract Task<bool> UninstallPackageAsync(PackageItem package);
        public abstract Task CloseAllPackageRelatedWindowsAsync(List<PackageItem> packages);
        public abstract Task SaveAllAsync();
        protected abstract Version MinVersion { get; }
        protected abstract Version MaxVersion { get; }
    }
}