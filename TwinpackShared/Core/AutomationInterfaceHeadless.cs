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
using System.Web.UI;
using System.Xml;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Models;
using Twinpack.Models.Api;
using VSLangProj;

namespace Twinpack.Core
{
    public class AutomationInterfaceHeadless : AutomationInterface, IAutomationInterface
    {
        public event EventHandler<ProgressEventArgs> ProgressedEvent = delegate { };

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static XNamespace TcNs = ConfigPlcProjectFactory.TcNs;

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
            var plcConfig = _config.Projects.FirstOrDefault(x => x.Name == package.ProjectName).Plcs.FirstOrDefault();

            var xdoc = XDocument.Load(plcConfig.FilePath);
            var project = xdoc.Elements(TcNs + "Project").FirstOrDefault();
            if (project == null)
                throw new InvalidDataException($"{plcConfig.FilePath} is not a valid plcproj file");

            // get or create groups
            var resolutionsGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderResolution") != null).FirstOrDefault();
            var referencesGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderReference") != null).FirstOrDefault();
            var libraryGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "LibraryReference") != null).FirstOrDefault();

            if (package.Config?.Options?.LibraryReference == true && package.Config.Version != null)
            {
                if (libraryGroup == null)
                {
                    libraryGroup = new XElement(TcNs + "ItemGroup");
                    project.Add(libraryGroup);
                }

                referencesGroup.Add(
                    new XElement(TcNs + "PlaceholderReference",
                        new XAttribute("Include", $"{package.Config.Name},{package.Config.Version},{package.Config.DistributorName}"),
                        new List<XElement> {
                            new XElement(TcNs + "Namespace", package.Config.Name),
                        }
                    )
                );
            }
            else
            {
                if (resolutionsGroup == null)
                {
                    resolutionsGroup = new XElement(TcNs + "ItemGroup");
                    project.Add(resolutionsGroup);
                }

                if (referencesGroup == null)
                {
                    referencesGroup = new XElement(TcNs + "ItemGroup");
                    project.Add(referencesGroup);
                }

                referencesGroup.Add(
                    new XElement(TcNs + "PlaceholderReference",
                        new XAttribute("Include", package.Config.Name),
                        new List<XElement> {
                            new XElement(TcNs + "DefaultResolution", $"{package.Config.Name}, {(package.Config.Version ?? "*")} ({package.Config.DistributorName})"),
                            new XElement(TcNs + "Namespace", package.Config.Name),
                        }
                    )
                );

                resolutionsGroup.Add(
                    new XElement(TcNs + "PlaceholderResolution",
                        new XAttribute("Include", package.Config.Name),
                        new XElement(TcNs + "Resolution", $"{package.Config.Name}, {(package.Config.Version ?? "*")} ({package.Config.DistributorName})")
                     )
                );
            }

            xdoc.Save(plcConfig.FilePath);
        }

        public override async Task RemovePackageAsync(PackageItem package, bool uninstall=false)
        {
            var plcConfig = _config.Projects.FirstOrDefault(x => x.Name == package.ProjectName).Plcs.FirstOrDefault();

            var xdoc = XDocument.Load(plcConfig.FilePath);
            var project = xdoc.Elements(TcNs + "Project").FirstOrDefault();
            if (project == null)
                throw new InvalidDataException($"{plcConfig.FilePath} is not a valid plcproj file");

            // get or create groups
            var resolutionsGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderResolution") != null).FirstOrDefault();
            var referencesGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderReference") != null).FirstOrDefault();
            var libraryGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "LibraryReference") != null).FirstOrDefault();

            var re = new Regex(@"(.*?),(.*?) \((.*?)\)");
            foreach (XElement g in xdoc.Elements(TcNs + "Project")?.Elements(TcNs + "ItemGroup")?.Elements(TcNs + "PlaceholderResolution")?.Elements(TcNs + "Resolution") ?? new List<XElement>())
            {
                var match = re.Match(g.Value);
                if (match.Success)
                {
                    var library = new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim() == "*" ? null : match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() };
                    if (library.Name == package.Config.Name)
                        g.Remove();
                }
            }

            foreach (XElement g in xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "PlaceholderReference").Elements(TcNs + "DefaultResolution"))
            {
                var match = re.Match(g.Value);
                if (match.Success)
                {
                    var library = new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim() == "*" ? null : match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() };
                    if (library.Name == package.Config.Name)
                        g.Remove();
                }
            }

            re = new Regex(@"(.*?),(.*?),(.*?)");
            foreach (XElement g in xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "LibraryReference"))
            {
                var libraryReference = g.Attribute("Include").Value.ToString();
                if (libraryReference == null)
                    continue;

                var match = re.Match(libraryReference);
                if (match.Success)
                {
                    var library = new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim() == "*" ? null : match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() };
                    if (library.Name == package.Config.Name)
                        g.Remove();
                }
            }

            xdoc.Save(plcConfig.FilePath);
        }

        public override async Task InstallPackageAsync(PackageItem package, string cachePath = null)
        {
            _logger.Warn("Headless AutomationInterface can not install packages");
        }

        public override async Task<bool> UninstallPackageAsync(PackageItem package)
        {
            _logger.Warn("Headless AutomationInterface can not uninstall packages");
            return false;
        }
    }
}