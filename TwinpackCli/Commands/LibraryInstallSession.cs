#if TWINPACK_CLI_FRAMEWORK
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using Twinpack;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;

namespace Twinpack.Commands
{
    /// <summary>
    /// Opens one hidden XAE Shell on the cached host solution and installs every library
    /// in the batch. Callers pass the whole set. This never opens a solution per package.
    /// </summary>
    static class LibraryInstallSession
    {
        static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public const string ProjectName = "InstallHost";
        public const string PlcName = "Plc";

        public static string TemplateDirectory()
        {
            return Path.Combine(AppContext.BaseDirectory, "InstallHost");
        }

        public static string EnsureCachedHost()
        {
            var cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Zeugwerk", "Twinpack", "install-host");
            var solution = Path.Combine(cache, "InstallHost.sln");
            var plc = Path.Combine(cache, "InstallHost", "Plc", "Plc.plcproj");
            if (File.Exists(solution) && File.Exists(plc))
                return cache;

            var template = TemplateDirectory();
            if (!File.Exists(Path.Combine(template, "InstallHost.sln")))
                throw new DirectoryNotFoundException("Install host solution was not found next to twinpack (" + template + ").");

            CopyAll(template, cache);
            return cache;
        }

        public static void InstallAll(IReadOnlyList<PackageItem> packages, string cachePath)
        {
            if (packages == null || packages.Count == 0)
            {
                _logger.Info("[install] no libraries to install");
                return;
            }

            var host = EnsureCachedHost();
            _logger.Info("[install] {0} library(ies), one XAE Shell, host {1}", packages.Count, LogPath.Display(host));

            var config = new Config
            {
                Solution = "InstallHost.sln",
                WorkingDirectory = host,
                Projects = new System.Collections.Generic.List<ConfigProject>
                {
                    new ConfigProject
                    {
                        Name = ProjectName,
                        Plcs = new System.Collections.Generic.List<ConfigPlcProject>
                        {
                            new ConfigPlcProject { Name = PlcName }
                        }
                    }
                }
            };

            // One shell for the whole batch. Dispose quits XAE when the loop finishes.
            var visualStudio = new VisualStudio(hidden: true);
            try
            {
                var automation = visualStudio.Open(config, "TC3.1", removeUnlistedProjects: false);
                try
                {
                    visualStudio.SaveAll();
                }
                catch (Exception ex)
                {
                    _logger.Warn("[install] host solution was not saved: {0}", ex.Message);
                    _logger.Trace(ex);
                }

                foreach (var package in packages)
                {
                    package.ProjectName = ProjectName;
                    package.PlcName = PlcName;

                    var name = package.PackageVersion?.Name ?? package.PackageVersion?.Title;
                    var version = package.PackageVersion?.Version;
                    _logger.Info("[install] {0} {1}", name, version);
                    try
                    {
                        automation.InstallPackageAsync(package, cachePath).GetAwaiter().GetResult();
                    }
                    catch (Exception ex) when (!CancelableTask.IsCancellation(ex))
                    {
                        _logger.Error("[install] {0} {1} failed: {2}", name, version, ex.Message);
                        _logger.Trace(ex);
                    }
                }
            }
            finally
            {
                visualStudio.Dispose();
            }
        }

        static void CopyAll(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);

            foreach (var directory in Directory.GetDirectories(source))
                CopyAll(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
#endif
