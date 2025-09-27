using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Twinpack.Configuration;
using Twinpack.Models;

namespace Twinpack.Core
{
    public class AutomationInterfaceHeadless : AutomationInterface, IAutomationInterface
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        protected static XNamespace TcNs = ConfigPlcProjectFactory.TcNs;

        protected Config _config;

        public AutomationInterfaceHeadless(Config config)
        {
            _config = config;
        }

        protected override Version MinVersion => new Version(3, 1, 4024, 0);
        protected override Version MaxVersion => null;

        public override string SolutionPath { get => Path.GetFullPath(_config.WorkingDirectory); }

        public override async System.Threading.Tasks.Task SaveAllAsync()
        {
            ;
        }

        public override async Task<string> ResolveEffectiveVersionAsync(string projectName, string plcName, string placeholderName)
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

        public override async System.Threading.Tasks.Task CloseAllPackageRelatedWindowsAsync(List<PackageItem> packages)
        {
            
        }

        public override async System.Threading.Tasks.Task AddPackageAsync(PackageItem package)
        {
            await AddPackageAsync(package, ns: package.PackageVersion.Title);
        }

        public async System.Threading.Tasks.Task AddPackageAsync(PackageItem package, string ns)
        {
            static void AddOptions(XElement root, AddPlcLibraryOptions options)
            {
                if (options?.Optional == true)
                    root.Add(new XElement(TcNs + "Optional", options.Optional.ToString().ToLower()));

                if (options?.HideWhenReferencedAsDependency == true)
                    root.Add(new XElement(TcNs + "HideWhenReferencedAsDependency", options.HideWhenReferencedAsDependency.ToString().ToLower()));

                if (options?.PublishSymbolsInContainer == true)
                    root.Add(new XElement(TcNs + "PublishSymbolsInContainer", options.PublishSymbolsInContainer.ToString().ToLower()));

                if (options?.QualifiedOnly == true)
                    root.Add(new XElement(TcNs + "QualifiedOnly", options.QualifiedOnly.ToString().ToLower()));
            }

            // make sure the package is not present before adding it, we have to
            // force, because the package might not even be installed
            await RemovePackageAsync(package, forceRemoval: true);

            var plcConfig = _config.Projects.FirstOrDefault(x => x.Name == package.ProjectName).Plcs.FirstOrDefault(x => x.Name == package.PlcName);
            if (plcConfig.FilePath == null || !File.Exists(plcConfig.FilePath))
                throw new FileNotFoundException($"Plc '{plcConfig.Name}' can not be found {(plcConfig.FilePath == null ? "" : "in " + plcConfig.FilePath)}");

            var xdoc = XDocument.Load(plcConfig.FilePath);
            var project = xdoc.Elements(TcNs + "Project").FirstOrDefault();
            if (project == null)
                throw new InvalidDataException($"{plcConfig.FilePath} is not a valid plcproj file");

            // get or create groups
            var resolutionsGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderResolution").Any()).FirstOrDefault();
            var referencesGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderReference").Any()).FirstOrDefault();
            var libraryGroup = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "LibraryReference").Any()).FirstOrDefault();

            if (package.Config?.Options?.LibraryReference == true)
            {
                if (libraryGroup == null)
                {
                    libraryGroup = new XElement(TcNs + "ItemGroup");
                    project.Add(libraryGroup);
                }

                var library = new XElement(TcNs + "LibraryReference",
                        new XAttribute("Include", $"{package.PackageVersion.Title},{(package.PackageVersion.Version ?? "*")},{package.PackageVersion.DistributorName}"),
                        new List<XElement> {
                            new XElement(TcNs + "Namespace", ns),
                        });

                AddOptions(library, package.Config?.Options);
                referencesGroup.Add(library);
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

                var reference = new XElement(TcNs + "PlaceholderReference",
                        new XAttribute("Include", package.PackageVersion.Title),
                        new List<XElement> {
                            new XElement(TcNs + "DefaultResolution", $"{package.PackageVersion.Title}, {(package.PackageVersion.Version ?? "*")} ({package.PackageVersion.DistributorName})"),
                            new XElement(TcNs + "Namespace", ns),
                        }
                    );

                AddOptions(reference, package.Config?.Options);
                referencesGroup.Add(reference);

                resolutionsGroup.Add(
                    new XElement(TcNs + "PlaceholderResolution",
                        new XAttribute("Include", package.PackageVersion.Title),
                        new XElement(TcNs + "Resolution", $"{package.PackageVersion.Title}, {(package.PackageVersion.Version ?? "*")} ({package.PackageVersion.DistributorName})")
                     )
                );
            }

            xdoc.Save(plcConfig.FilePath);
        }

        public override async System.Threading.Tasks.Task RemovePackageAsync(PackageItem package, bool uninstall = false, bool forceRemoval = false)
        {
            var plcConfig = _config?.Projects?.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
            if (plcConfig == null)
                throw new InvalidOperationException($"Project '{package.ProjectName}' (Plc {package.PlcName}) is not configured in {_config?.FilePath}");

            if (plcConfig.FilePath == null || !File.Exists(plcConfig.FilePath))
                throw new FileNotFoundException($"Plc '{plcConfig.Name}' can not be found {(plcConfig.FilePath == null ? "" : "in " + plcConfig.FilePath)}");

            var xdoc = XDocument.Load(plcConfig.FilePath);
            var project = xdoc.Elements(TcNs + "Project").FirstOrDefault();
            if (project == null)
                throw new InvalidDataException($"{plcConfig.FilePath} is not a valid plcproj file");

            var re = new Regex(@"(.*?),(.*?) \((.*?)\)");
            foreach (XElement g in xdoc.Elements(TcNs + "Project")?.Elements(TcNs + "ItemGroup")?.Elements(TcNs + "PlaceholderResolution")?.Elements(TcNs + "Resolution") ?? new List<XElement>())
            {
                var match = re.Match(g.Value);
                if (match.Success)
                {
                    var library = new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim() == "*" ? null : match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() };
                    if (library.Name == package.PackageVersion.Title)
                        g.Parent.Remove();
                }
            }

            foreach (XElement g in xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "PlaceholderReference").Elements(TcNs + "DefaultResolution"))
            {
                var match = re.Match(g.Value);
                if (match.Success)
                {
                    var library = new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim() == "*" ? null : match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() };
                    if (library.Name == package.PackageVersion.Title)
                        g.Parent.Remove();
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
                    if (library.Name == package.PackageVersion.Title)
                        g.Remove();
                }
            }

            xdoc.Save(plcConfig.FilePath);
        }

        public override async System.Threading.Tasks.Task RemoveAllPackagesAsync(string projectName, string plcName)
        {
            var plcConfig = _config.Projects.FirstOrDefault(x => x.Name == projectName).Plcs.FirstOrDefault(x => x.Name == plcName);

            if (plcConfig.FilePath == null || !File.Exists(plcConfig.FilePath))
                throw new FileNotFoundException($"Plc '{plcConfig.Name}' can not be found {(plcConfig.FilePath == null ? "" : "in " + plcConfig.FilePath)}");

            var xdoc = XDocument.Load(plcConfig.FilePath);
            var project = xdoc.Elements(TcNs + "Project").FirstOrDefault();
            if (project == null)
                throw new InvalidDataException($"{plcConfig.FilePath} is not a valid plcproj file");

            XElement element = null;
            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderResolution")?.Any() == true)?.FirstOrDefault()) != null)
                element.Remove();

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderReference")?.Any() == true)?.FirstOrDefault()) != null)
                element.Remove();

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "LibraryReference")?.Any() == true)?.FirstOrDefault()) != null)
                element.Remove();

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(e => !e.HasElements).FirstOrDefault()) != null)
                element.Remove();

            xdoc.Save(plcConfig.FilePath);
        }

        public override async System.Threading.Tasks.Task InstallPackageAsync(PackageItem package, string cachePath = null)
        {
            _logger.Warn("Headless AutomationInterface can not install packages");
        }

        public override async Task<bool> UninstallPackageAsync(PackageItem package)
        {
            _logger.Warn("Headless AutomationInterface can not uninstall packages");
            return false;
        }

        public override async System.Threading.Tasks.Task SetPackageVersionAsync(ConfigPlcProject plc, CancellationToken cancellationToken = default)
        {
            if (plc.FilePath == null || !File.Exists(plc.FilePath))
                throw new FileNotFoundException($"Plc '{plc.Name}' can not be found {(plc.FilePath == null ? "" : "in " + plc.FilePath)}");

            var xdoc = XDocument.Load(plc.FilePath);
            var project = xdoc.Elements(TcNs + "Project").FirstOrDefault();
            if (project == null)
                throw new InvalidDataException($"{plc.FilePath} is not a valid plcproj file");


            XElement propertyGroup = project.Elements(TcNs + "PropertyGroup").FirstOrDefault();
            if (propertyGroup != null)
            {
                var title = propertyGroup.Elements(TcNs + "Title").FirstOrDefault();
                var name = propertyGroup.Elements(TcNs + "Name").FirstOrDefault();
                var projectVersion = propertyGroup.Elements(TcNs + "Version")?.FirstOrDefault()
                                ?? propertyGroup.Elements(TcNs + "ProjectVersion").FirstOrDefault();
                var company = propertyGroup.Elements(TcNs + "Company").FirstOrDefault();

                var allowedPlcNameRegex = new Regex("^[a-zA-Z]+[a-zA-Z0-9_]+$");
                var allowedCompanyRegex = new Regex("^.*$");
                if (!string.IsNullOrEmpty(/*plc.Title ?? */plc.Name) && allowedPlcNameRegex.IsMatch(plc.Name))
                {
                    if (name != null)
                        name.Value = plc.Name;
                    else
                        propertyGroup.Add(new XElement(TcNs + "Name", plc.Name));

                    _logger.Info($"Updated name to '{plc.Name}'");
                }
                else
                {
                    _logger.Warn($"Name '{plc.Name}' contains invalid characters - skipping PLC Name update, the package might be broken!");

                }

                // config.packages do not have a title, only a name ...
                var titleStr = plc.Title ?? plc.Name;
                if (!string.IsNullOrEmpty(titleStr) && allowedPlcNameRegex.IsMatch(titleStr))
                {
                    if (title != null)
                        title.Value = titleStr;
                    else
                        propertyGroup.Add(new XElement(TcNs + "Title", titleStr));

                    _logger.Info($"Updated title to '{titleStr}'");
                }
                else if ((plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary || plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary) && string.IsNullOrEmpty(titleStr))
                {
                    throw new ArgumentException("Title is empty, but it is mandatory for libraries!");
                }
                else
                {
                    _logger.Warn($"Title '{titleStr}' contains invalid characters - skipping PLC title update, the package might be broken!");

                }

                if (!string.IsNullOrEmpty(plc.DistributorName) && allowedCompanyRegex.IsMatch(plc.DistributorName))
                {
                    if (company != null)
                        company.Value = plc.DistributorName;
                    else
                        propertyGroup.Add(new XElement(TcNs + "Company", plc.DistributorName));

                    _logger.Info($"Updated company to '{plc.DistributorName}'");
                }
                else if((plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary || plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary) && string.IsNullOrEmpty(plc.DistributorName))
                {
                    throw new ArgumentException("Distributor name is empty, but it is mandatory for libraries!");
                }
                else
                {
                    _logger.Warn($"Distributor name '{plc.DistributorName}' contains invalid characters - skipping PLC company update, the package might be broken!");
                }

                var versionStr = NormalizedVersion(plc.Version);
                if (!string.IsNullOrEmpty(versionStr))
                {
                    if (projectVersion != null)
                        projectVersion.Value = versionStr;
                    else
                        propertyGroup.Add(new XElement(TcNs + "ProjectVersion", versionStr));

                    _logger.Info($"Updated version to '{versionStr}'");
                }
                else if ((plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary || plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary) && string.IsNullOrEmpty(versionStr))
                {
                    throw new ArgumentException("Version is empty, but it is mandatory for libraries!");
                }
                else
                {
                    _logger.Warn($"Version '{versionStr}' is empty - skipping PLC company update, the package might be broken!");
                }
            }

            xdoc.Save(plc.FilePath);
        }

        public override void SaveAsLibrary(ConfigPlcProject plc, string filePath) { }
    }
}
