using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Twinpack.Configuration;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace Twinpack.Core
{
    public class AutomationInterfaceHeadless : AutomationInterface, IAutomationInterface
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        protected static XNamespace TcNs = ConfigPlcProjectFactory.TcNs;
        protected static List<XElement> LocalRepository;

        protected Config _config;
        string _productLine;

        public AutomationInterfaceHeadless(Config config)
        {
            _config = config;
        }

        public override string SolutionProductLine => _productLine;

        /// <summary>
        /// <paramref name="requestedTcVersion"/> is the build's <c>--tcversion</c>.
        /// <c>TC3.1.4024</c> and <c>TC3.1.4026</c> select that product line.
        /// <c>TC3.1</c> does not, because it names neither build.
        /// </summary>
        public void UseProductLine(string requestedTcVersion)
        {
            ConfigWhen.EnsureCheckable(_config, requestedTcVersion);

            var line = TwinCatTargets.ProductLine(requestedTcVersion);
            _productLine = string.IsNullOrEmpty(line) || TwinCatTargets.IsUnpinned(line) ? null : line;
            if (!string.IsNullOrEmpty(_productLine))
                _logger.Info("[target] product line {0}", _productLine);
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

        private static readonly string[] _configPaths = new[]
        {
            @"C:\ProgramData\Beckhoff\TwinCAT\PlcEngineering\Managed Libraries\cache",
            @"C:\TwinCAT\3.1\Components\Plc\Managed Libraries\cache"
        };

        private IEnumerable<XElement> LoadLibraryElements()
        {
            foreach (var file in _configPaths)
            {
                if (!File.Exists(file))
                    continue;

                XDocument doc;
                try { doc = XDocument.Load(file); }
                catch { continue; }

                foreach (var element in doc.Descendants("Library"))
                    yield return element;
            }
        }

        private XElement? FindMatch(PackageVersionGetResponse pv, bool requireDistributor)
        {
            // An unpinned request takes whatever is installed, so it must not be compared against the
            // repository entry literally. "*" is how a plcproj reference spells that, next to null and
            // empty, and every one of them has to be accepted here or the library is reported missing.
            var unpinned = string.IsNullOrEmpty(pv.Version) || pv.Version == "*";

            return LocalRepository.FirstOrDefault(lib =>
            {
                var title = (string)lib.Attribute("Title");
                var company = (string)lib.Attribute("Company");
                var version = (string)lib.Attribute("Version");

                return string.Equals(title, pv.Title, StringComparison.InvariantCultureIgnoreCase)
                    && (!requireDistributor || string.Equals(company, pv.DistributorName, StringComparison.InvariantCultureIgnoreCase))
                    && (unpinned || TwinCATLibraryVersionsEquivalent(version, pv.Version));
            });
        }

        private XElement? FindInstalled(PackageVersionGetResponse pv)
        {
            LocalRepository ??= LoadLibraryElements().ToList();

            if (string.IsNullOrEmpty(pv.DistributorName))
                return FindMatch(pv, requireDistributor: false);

            return FindMatch(pv, requireDistributor: true)
                ?? FindMatch(pv, requireDistributor: false);
        }

        /// <summary>
        /// The version an installed library is registered under when <paramref name="pv"/> names it
        /// with a different spelling, otherwise null. A NuGet feed publishes a library versioned
        /// <c>1.2.3.0</c> as <c>1.2.3</c>, and a package without a <c>tp-library-version</c> tag does
        /// not say which of the two it is. TwinCAT resolves a reference only under the exact string,
        /// so an installed library is referenced the way TwinCAT registered it. An installed library
        /// with exactly the requested version wins over one that is only equivalent, the same rule
        /// the Visual Studio automation interface applies against the library manager.
        /// </summary>
        private string InstalledLibraryVersion(PackageVersionGetResponse pv)
        {
            if (string.IsNullOrEmpty(pv?.Version) || pv.Version == "*" || string.IsNullOrEmpty(pv.Title))
                return null;

            string wanted;
            try
            {
                wanted = TwinCATLibraryVersion(pv.Version);
            }
            catch (ArgumentException)
            {
                return null;
            }

            LocalRepository ??= LoadLibraryElements().ToList();
            var candidates = LocalRepository
                .Where(lib => string.Equals((string)lib.Attribute("Title"), pv.Title, StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            if (!string.IsNullOrEmpty(pv.DistributorName))
            {
                var sameCompany = candidates
                    .Where(lib => string.Equals((string)lib.Attribute("Company"), pv.DistributorName, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();
                if (sameCompany.Count > 0)
                    candidates = sameCompany;
            }

            var versions = candidates
                .Select(lib => (string)lib.Attribute("Version"))
                .Where(version => !string.IsNullOrEmpty(version))
                .ToList();
            if (versions.Any(version => string.Equals(version, wanted, StringComparison.Ordinal)))
                return null;

            return versions.FirstOrDefault(version => TwinCATLibraryVersionsEquivalent(version, wanted));
        }

        public override bool IsPackageInstalled(PackageItem package)
        {
            return FindInstalled(package.PackageVersion) != null;
        }

        public override async Task<bool> IsPackageInstalledAsync(PackageItem package)
        {
            return await Task.Run(() => IsPackageInstalled(package));
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

            static void AddParameters(XElement root, Dictionary<string, string> parameters)
            {
                if (parameters == null || parameters.Count == 0)
                    return;

                _logger.Info("[parameters] applying {0} parameter(s) to placeholder '{1}'", parameters.Count, root.Attribute("Include")?.Value);

                var parametersElement = root.Element(TcNs + "Parameters");
                if (parametersElement == null)
                {
                    parametersElement = new XElement(TcNs + "Parameters");
                    root.Add(parametersElement);
                }

                parametersElement.RemoveAll();

                foreach (var parameter in parameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter.Key))
                        continue;

                    var keyParts = parameter.Key.Split(new[] { '.' }, 2, StringSplitOptions.None);
                    if (keyParts.Length != 2 || string.IsNullOrWhiteSpace(keyParts[0]) || string.IsNullOrWhiteSpace(keyParts[1]))
                    {
                        _logger.Warn("[parameters] skipping invalid key '{0}' (expected LISTNAME.KEY)", parameter.Key);
                        continue;
                    }

                    _logger.Info($"Adding package parameter '{keyParts[0]}.{keyParts[1]}'='{parameter.Value ?? string.Empty}'");
                    parametersElement.Add(new XElement("Parameter",
                        new XAttribute("ListName", keyParts[0]),
                        new XElement("Key", keyParts[1]),
                        new XElement("Value", parameter.Value ?? string.Empty)));
                }
            }

            ns = ns.Replace(" ", "_");

            // Written back to the package, so config.json records the version the reference uses.
            var installedVersion = InstalledLibraryVersion(package.PackageVersion);
            if (installedVersion != null)
            {
                _logger.Info("[reference] {0} {1} is installed as {2}, referencing that version",
                    package.PackageVersion.Title, package.PackageVersion.Version, installedVersion);
                package.PackageVersion.Version = WithLibraryVersion(package.PackageVersion.Version, installedVersion);
                package.PackageVersion.VersionAmbiguous = false;
            }

            var match = FindInstalled(package.PackageVersion);
            var distributorName = match?.Attribute("Company")?.Value ?? package.PackageVersion.DistributorName;
            ns = match?.Attribute("DefaultNamespace")?.Value ?? ns;

            // Without a distributor the reference is written as "Name, * ()", which TwinCAT reports much
            // later as a library that "has not been installed to the system". Say so here instead.
            if (string.IsNullOrEmpty(distributorName))
                throw new InvalidOperationException(
                    $"Cannot resolve the distributor of '{package.PackageVersion.Title}': it states none and no " +
                    $"TwinCAT library repository index ({string.Join(", ", _configPaths)}) lists it");

            // an explicitly configured namespace (e.g. preserved from a previous reference) wins over the computed default
            var namespaceOverride = package.Config?.Namespace;
            if (!string.IsNullOrEmpty(namespaceOverride) && !ConfigPlcPackage.IsUsableNamespace(namespaceOverride))
            {
                _logger.Warn("[namespace] ignoring '{0}' configured for {1}, it is no valid identifier", namespaceOverride, package.PackageVersion.Name);
                namespaceOverride = null;
            }

            ns = string.IsNullOrEmpty(namespaceOverride) ? ns : namespaceOverride;

            // make sure the package is not present before adding it, we have to
            // force, because the package might not even be installed.
            // Children this writer does not own (SystemLibrary, and whatever Beckhoff adds next)
            // are copied off the old element first. The add API cannot set them.
            var preservedChildren = UnmanagedReferenceChildren(package);
            await RemovePackageAsync(package, forceRemoval: true);

            var plcConfig = _config.Projects.FirstOrDefault(x => x.Name == package.ProjectName).Plcs.FirstOrDefault(x => x.Name == package.PlcName);
            if (plcConfig.FilePath == null || !File.Exists(plcConfig.FilePath))
                throw new FileNotFoundException($"Plc '{plcConfig.Name}' can not be found {(plcConfig.FilePath == null ? "" : "in " + plcConfig.FilePath)}");

            var xdoc = LoadPlcProject(plcConfig.FilePath);
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
                    libraryGroup = CreateItemGroup(project, before: null);

                var library = new XElement(TcNs + "LibraryReference",
                        new XAttribute("Include", $"{package.PackageVersion.Title},{(TwinCATLibraryVersion(package.PackageVersion.Version) ?? "*")},{distributorName}"),
                        new List<XElement> {
                            new XElement(TcNs + "Namespace", ns),
                        });

                AddOptions(library, package.Config?.Options);
                foreach (var child in preservedChildren)
                    library.Add(new XElement(child));
                var libraryTemplate = libraryGroup.Elements(TcNs + "LibraryReference").FirstOrDefault()
                    ?? referencesGroup?.Elements(TcNs + "PlaceholderReference").FirstOrDefault();
                if (libraryTemplate != null)
                    FormatLike(libraryTemplate, library);
                else
                    FormatInNewGroup(libraryGroup, library);
                AppendPlcItem(libraryGroup, library);
            }
            else
            {
                // TwinCAT keeps the references group in front of the resolutions group.
                if (referencesGroup == null)
                    referencesGroup = CreateItemGroup(project, before: resolutionsGroup);

                if (resolutionsGroup == null)
                    resolutionsGroup = CreateItemGroup(project, before: null, after: referencesGroup);

                var reference = new XElement(TcNs + "PlaceholderReference",
                        new XAttribute("Include", package.PackageVersion.Title),
                        new List<XElement> {
                            // The placeholder default stays "*". A version in the config is the project pin
                            // and is written into Resolution below, not into the default.
                            new XElement(TcNs + "DefaultResolution", $"{package.PackageVersion.Title}, * ({distributorName})"),
                            new XElement(TcNs + "Namespace", ns),
                        }
                    );

                AddOptions(reference, package.Config?.Options);
                AddParameters(reference, package.Config?.Parameters);
                foreach (var child in preservedChildren)
                    reference.Add(new XElement(child));
                var referenceTemplate = referencesGroup.Elements(TcNs + "PlaceholderReference").FirstOrDefault();
                if (referenceTemplate != null)
                    FormatLike(referenceTemplate, reference);
                else
                    FormatInNewGroup(referencesGroup, reference);
                AppendPlcItem(referencesGroup, reference);

                var resolution = new XElement(TcNs + "PlaceholderResolution",
                        new XAttribute("Include", package.PackageVersion.Title),
                        new XElement(TcNs + "Resolution", $"{package.PackageVersion.Title}, {(TwinCATLibraryVersion(package.PackageVersion.Version) ?? "*")} ({distributorName})")
                     );
                var resolutionTemplate = resolutionsGroup.Elements(TcNs + "PlaceholderResolution").FirstOrDefault();
                if (resolutionTemplate != null)
                    FormatLike(resolutionTemplate, resolution);
                else
                    FormatInNewGroup(resolutionsGroup, resolution);
                AppendPlcItem(resolutionsGroup, resolution);
            }

            if (preservedChildren.Count > 0)
            {
                _logger.Info("[reference] keeping {0} existing element(s) on {1}: {2}",
                    preservedChildren.Count,
                    package.PackageVersion.Title,
                    string.Join(", ", preservedChildren.Select(DescribePreservedChild)));
            }

            SavePlcProject(xdoc, plcConfig.FilePath);
        }

        public override async System.Threading.Tasks.Task RemovePackageAsync(PackageItem package, bool uninstall = false, bool forceRemoval = false)
        {
            var plcConfig = _config?.Projects?.FirstOrDefault(x => x.Name == package.ProjectName)?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
            if (plcConfig == null)
                throw new InvalidOperationException($"Project '{package.ProjectName}' (Plc {package.PlcName}) is not configured in {_config?.FilePath}");

            if (plcConfig.FilePath == null || !File.Exists(plcConfig.FilePath))
                throw new FileNotFoundException($"Plc '{plcConfig.Name}' can not be found {(plcConfig.FilePath == null ? "" : "in " + plcConfig.FilePath)}");

            var xdoc = LoadPlcProject(plcConfig.FilePath);
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
                        RemovePlcItem(g.Parent);
                }
            }

            foreach (XElement g in xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "PlaceholderReference").Elements(TcNs + "DefaultResolution"))
            {
                var match = re.Match(g.Value);
                if (match.Success)
                {
                    var library = new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim() == "*" ? null : match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() };
                    if (library.Name == package.PackageVersion.Title)
                        RemovePlcItem(g.Parent);
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
                        RemovePlcItem(g);
                }
            }

            SavePlcProject(xdoc, plcConfig.FilePath);
        }

        public override async System.Threading.Tasks.Task RemoveAllPackagesAsync(string projectName, string plcName)
        {
            var plcConfig = _config.Projects.FirstOrDefault(x => x.Name == projectName).Plcs.FirstOrDefault(x => x.Name == plcName);

            if (plcConfig.FilePath == null || !File.Exists(plcConfig.FilePath))
                throw new FileNotFoundException($"Plc '{plcConfig.Name}' can not be found {(plcConfig.FilePath == null ? "" : "in " + plcConfig.FilePath)}");

            var xdoc = LoadPlcProject(plcConfig.FilePath);
            var project = xdoc.Elements(TcNs + "Project").FirstOrDefault();
            if (project == null)
                throw new InvalidDataException($"{plcConfig.FilePath} is not a valid plcproj file");

            XElement element = null;
            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderResolution")?.Any() == true)?.FirstOrDefault()) != null)
                RemovePlcItem(element);

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderReference")?.Any() == true)?.FirstOrDefault()) != null)
                RemovePlcItem(element);

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "LibraryReference")?.Any() == true)?.FirstOrDefault()) != null)
                RemovePlcItem(element);

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(e => !e.HasElements).FirstOrDefault()) != null)
                RemovePlcItem(element);

            SavePlcProject(xdoc, plcConfig.FilePath);
        }

        public override async System.Threading.Tasks.Task InstallPackageAsync(PackageItem package, string cachePath = null)
        {
            _logger.Warn("[install] headless mode cannot install packages");
        }

        public override async Task<bool> UninstallPackageAsync(PackageItem package)
        {
            _logger.Warn("[remove] headless mode cannot uninstall packages");
            return false;
        }

        public override async System.Threading.Tasks.Task SetPackageVersionAsync(ConfigPlcProject plc, CancellationToken cancellationToken = default)
        {
            if (plc.FilePath == null || !File.Exists(plc.FilePath))
                throw new FileNotFoundException($"Plc '{plc.Name}' can not be found {(plc.FilePath == null ? "" : "in " + plc.FilePath)}");

            var xdoc = LoadPlcProject(plc.FilePath);
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
                        AppendPlcItem(propertyGroup, new XElement(TcNs + "Name", plc.Name));

                    _logger.Info("[plc] updated name to {0}", plc.Name);
                }
                else
                {
                    _logger.Warn("[plc] name '{0}' has invalid characters, skipping", plc.Name);

                }

                // config.packages do not have a title, only a name ...
                var titleStr = plc.Title ?? plc.Name;
                if (!string.IsNullOrEmpty(titleStr) && allowedPlcNameRegex.IsMatch(titleStr))
                {
                    if (title != null)
                        title.Value = titleStr;
                    else
                        AppendPlcItem(propertyGroup, new XElement(TcNs + "Title", titleStr));

                    _logger.Info("[plc] updated title to '{0}'", titleStr);
                }
                else if ((plc.PlcType == ConfigPlcProject.PlcProjectType.Library || plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary) && string.IsNullOrEmpty(titleStr))
                {
                    throw new ArgumentException("Title is empty, but it is mandatory for libraries!");
                }
                else
                {
                    _logger.Warn("[plc] title '{0}' has invalid characters, skipping", titleStr);

                }

                if (!string.IsNullOrEmpty(plc.DistributorName) && allowedCompanyRegex.IsMatch(plc.DistributorName))
                {
                    if (company != null)
                        company.Value = plc.DistributorName;
                    else
                        AppendPlcItem(propertyGroup, new XElement(TcNs + "Company", plc.DistributorName));

                    _logger.Info("[plc] updated company to {0}", plc.DistributorName);
                }
                else
                {
                    // Headed set-version (AutomationInterface4024) throws for a library without a distributor.
                    // Headless deliberately does not: a PLC without "type" counts as a framework library, and
                    // failing those would break existing pipelines. It warns instead.
                    var fallbackCompany = "Unknown Company";
                    if (plc.PlcType == ConfigPlcProject.PlcProjectType.Library || plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary)
                        _logger.Warn("[plc] {0} is a library without a distributor, its company is set to '{1}'", plc.Name, fallbackCompany);

                    if (company != null)
                        company.Value = fallbackCompany;
                    else
                        AppendPlcItem(propertyGroup, new XElement(TcNs + "Company", fallbackCompany));

                    _logger.Info("[plc] updated company to {0}", fallbackCompany);
                }

                var versionStr = TwinCATProjectVersion(plc.Version);
                if (!string.IsNullOrEmpty(versionStr))
                {
                    if (projectVersion != null)
                        projectVersion.Value = versionStr;
                    else
                        AppendPlcItem(propertyGroup, new XElement(TcNs + "ProjectVersion", versionStr));

                    _logger.Info("[plc] updated version to {0}", versionStr);
                }
                else if ((plc.PlcType == ConfigPlcProject.PlcProjectType.Library || plc.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary) && string.IsNullOrEmpty(versionStr))
                {
                    throw new ArgumentException("Version is empty, but it is mandatory for libraries!");
                }
                else
                {
                    _logger.Warn("[plc] version empty, skipping company update");
                }
            }

            SavePlcProject(xdoc, plc.FilePath);
        }

        static readonly HashSet<string> ManagedReferenceChildren = new HashSet<string>(StringComparer.Ordinal)
        {
            "DefaultResolution",
            "Namespace",
            "Parameters",
            "Optional",
            "HideWhenReferencedAsDependency",
            "PublishSymbolsInContainer",
            "QualifiedOnly"
        };

        // The library-manager API cannot recreate a flag a TwinCAT plug-in wrote, such as SystemLibrary.
        // Copy every child this writer does not emit itself, and leave a brand new reference without them.
        List<XElement> UnmanagedReferenceChildren(PackageItem package)
        {
            var plcConfig = _config?.Projects?.FirstOrDefault(x => x.Name == package.ProjectName)
                ?.Plcs?.FirstOrDefault(x => x.Name == package.PlcName);
            if (plcConfig?.FilePath == null || !File.Exists(plcConfig.FilePath))
                return new List<XElement>();

            var title = package.PackageVersion?.Title;
            if (string.IsNullOrEmpty(title))
                return new List<XElement>();

            var document = LoadPlcProject(plcConfig.FilePath);
            XElement existing;
            if (package.Config?.Options?.LibraryReference == true)
            {
                existing = document.Descendants(TcNs + "LibraryReference")
                    .FirstOrDefault(element => LibraryReferenceTitle(element) == title);
            }
            else
            {
                existing = document.Descendants(TcNs + "PlaceholderReference")
                    .FirstOrDefault(element => element.Attribute("Include")?.Value == title);
            }

            if (existing == null)
                return new List<XElement>();

            // Parameters from the config replace the ones in the plcproj. Without any in the config, the
            // plcproj's own (for example set in TwinCAT's library manager) are kept instead of dropped.
            var keepParameters = !(package.Config?.Parameters?.Count > 0);
            return existing.Elements()
                .Where(element => !ManagedReferenceChildren.Contains(element.Name.LocalName)
                    || (keepParameters && element.Name.LocalName == "Parameters"))
                .Select(element => new XElement(element))
                .ToList();
        }

        static string DescribePreservedChild(XElement element)
        {
            if (!element.HasElements)
                return element.Name.LocalName + "=" + element.Value;

            return element.Name.LocalName;
        }

        static string LibraryReferenceTitle(XElement reference)
        {
            var include = reference.Attribute("Include")?.Value ?? "";
            var comma = include.IndexOf(',');
            return (comma < 0 ? include : include.Substring(0, comma)).Trim();
        }

        // XDocument.Save writes a UTF-8 BOM and re-indents. A plcproj is compared in git, so a
        // one-reference edit has to keep the bytes of everything it did not change.
        static XDocument LoadPlcProject(string path)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = false,
                IgnoreComments = false,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using (var stream = File.OpenRead(path))
            using (var reader = XmlReader.Create(stream, settings))
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }

        static void SavePlcProject(XDocument document, string path)
        {
            var hasWhitespace = document.DescendantNodes().OfType<XText>()
                .Any(text => text.Value.Length > 0 && string.IsNullOrWhiteSpace(text.Value));
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: FileStartsWithUtf8Bom(path)),
                OmitXmlDeclaration = document.Declaration == null,
                Indent = !hasWhitespace,
                NewLineHandling = NewLineHandling.Replace,
                NewLineChars = DetectNewLine(path)
            };

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = XmlWriter.Create(stream, settings))
                document.Save(writer);
        }

        static bool FileStartsWithUtf8Bom(string path)
        {
            var preamble = Encoding.UTF8.GetPreamble();
            var buffer = new byte[preamble.Length];
            using (var stream = File.OpenRead(path))
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read < preamble.Length)
                    return false;

                for (var i = 0; i < preamble.Length; i++)
                {
                    if (buffer[i] != preamble[i])
                        return false;
                }
            }

            return true;
        }

        static string DetectNewLine(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                while (reader.Peek() >= 0)
                {
                    var ch = (char)reader.Read();
                    if (ch == '\r')
                        return "\r\n";
                    if (ch == '\n')
                        return "\n";
                }
            }

            return "\r\n";
        }

        // Removing an element leaves its indent behind. The next load joins that indent with the
        // next one, and copying the whole run on the following edit doubles the blank lines.
        static void RemovePlcItem(XElement element)
        {
            if (element == null)
                return;

            var group = element.Parent;
            var previous = element.PreviousNode as XText;
            element.Remove();
            if (previous != null && string.IsNullOrWhiteSpace(previous.Value))
                previous.Remove();

            if (group != null && !group.Elements().Any())
                RemovePlcItem(group);
        }

        // A loaded plcproj stores its indentation as whitespace text nodes. New elements are inserted
        // with one indent, matching their siblings, ahead of the closing tag's whitespace.
        static void AppendPlcItem(XElement parent, XElement child)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            var indent = parent.Elements()
                .Select(element => element.PreviousNode)
                .OfType<XText>()
                .FirstOrDefault(text => string.IsNullOrWhiteSpace(text.Value));
            var trailing = parent.LastNode as XText;
            var hasTrailing = trailing != null && string.IsNullOrWhiteSpace(trailing.Value);
            var indentText = OneIndent(indent?.Value) ?? parent.Annotation<NewGroupIndent>()?.Child;

            if (indentText != null && hasTrailing)
            {
                trailing.AddBeforeSelf(new XText(indentText));
                trailing.AddBeforeSelf(child);
                return;
            }

            if (indentText != null)
            {
                parent.Add(new XText(indentText));
                parent.Add(child);
                return;
            }

            parent.Add(child);
        }

        // The text node in front of an element can be several joined indents. Only the last line is the indent.
        static string OneIndent(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var lastNewLine = value.LastIndexOf('\n');
            if (lastNewLine < 0)
                lastNewLine = value.LastIndexOf('\r');
            if (lastNewLine <= 0)
                return value;

            var start = lastNewLine;
            if (value[start] == '\n' && start > 0 && value[start - 1] == '\r')
                start--;
            return value.Substring(start);
        }

        static void FormatLike(XElement template, XElement created)
        {
            if (template == null || created == null)
                return;

            var innerIndent = template.Elements()
                .Select(element => element.PreviousNode)
                .OfType<XText>()
                .FirstOrDefault(text => string.IsNullOrWhiteSpace(text.Value));
            if (innerIndent == null)
                return;

            var closing = template.LastNode as XText;
            var indentText = OneIndent(innerIndent.Value);
            Format(created, indentText, closing != null && string.IsNullOrWhiteSpace(closing.Value) ? closing.Value : null, IndentStep(template, indentText));
        }

        // Puts every child of a freshly built element on its own line with the given indent.
        static void Format(XElement created, string indentText, string closingText, string step)
        {
            var children = created.Elements().ToList();
            foreach (var child in children)
                child.Remove();

            foreach (var child in children)
            {
                created.Add(new XText(indentText));
                created.Add(child);
                if (step != null)
                    IndentNested(child, indentText, step);
            }

            if (closingText != null)
                created.Add(new XText(closingText));
        }

        // The indentation a newly created item group gives its children, as long as it has none to copy it from.
        sealed class NewGroupIndent
        {
            public string Child;
            public string Step;
        }

        // A new item group goes after the last existing one (in front of ProjectExtensions), or right before
        // or after the given group, and is indented like its sibling item groups.
        static XElement CreateItemGroup(XElement project, XElement before, XElement after = null)
        {
            var group = new XElement(TcNs + "ItemGroup");
            var projectIndent = OneIndent(project.Elements()
                .Select(element => element.PreviousNode)
                .OfType<XText>()
                .FirstOrDefault(text => string.IsNullOrWhiteSpace(text.Value))?.Value);
            var lastNewLine = projectIndent?.LastIndexOf('\n') ?? -1;
            var step = lastNewLine >= 0 ? projectIndent.Substring(lastNewLine + 1) : null;

            if (!string.IsNullOrEmpty(step))
            {
                group.Add(new XText(projectIndent));
                group.AddAnnotation(new NewGroupIndent { Child = projectIndent + step, Step = step });
            }

            after = before == null ? after ?? project.Elements(TcNs + "ItemGroup").LastOrDefault() : null;
            before = before ?? (after == null ? project.Elements(TcNs + "ProjectExtensions").FirstOrDefault() : null);

            if (after != null)
            {
                if (projectIndent != null)
                    after.AddAfterSelf(new XText(projectIndent), group);
                else
                    after.AddAfterSelf(group);
            }
            else if (before != null)
            {
                if (projectIndent != null)
                    before.AddBeforeSelf(group, new XText(projectIndent));
                else
                    before.AddBeforeSelf(group);
            }
            else
            {
                AppendPlcItem(project, group);
            }

            return group;
        }

        static void FormatInNewGroup(XElement group, XElement created)
        {
            var indent = group?.Annotation<NewGroupIndent>();
            if (indent == null || created == null)
                return;

            Format(created, indent.Child + indent.Step, indent.Child, indent.Step);
        }

        // One indentation level: how much further the template's children are indented than the template.
        static string IndentStep(XElement template, string childIndent)
        {
            var own = OneIndent((template.PreviousNode as XText)?.Value);
            if (string.IsNullOrEmpty(own) || childIndent == null || childIndent.Length <= own.Length || !childIndent.StartsWith(own, StringComparison.Ordinal))
                return null;

            return childIndent.Substring(own.Length);
        }

        // Lays out an element built without whitespace, such as Parameters, over several lines like the
        // rest of the file. An element that already holds text nodes (copied from the old reference, or
        // with a value) is left as it is.
        static void IndentNested(XElement element, string indent, string step)
        {
            if (!element.HasElements || element.Nodes().OfType<XText>().Any())
                return;

            var inner = indent + step;
            foreach (var child in element.Elements().ToList())
            {
                child.AddBeforeSelf(new XText(inner));
                IndentNested(child, inner, step);
            }

            element.Add(new XText(indent));
        }

        public override void SaveAsLibrary(ConfigPlcProject plc, string filePath) { }
    }
}
