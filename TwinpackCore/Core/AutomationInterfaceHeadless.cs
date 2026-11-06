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
                {
                    libraryGroup = new XElement(TcNs + "ItemGroup");
                    AppendPlcItem(project, libraryGroup);
                }

                var library = new XElement(TcNs + "LibraryReference",
                        new XAttribute("Include", $"{package.PackageVersion.Title},{(TwinCATLibraryVersion(package.PackageVersion.Version) ?? "*")},{distributorName}"),
                        new List<XElement> {
                            new XElement(TcNs + "Namespace", ns),
                        });

                AddOptions(library, package.Config?.Options);
                foreach (var child in preservedChildren)
                    library.Add(new XElement(child));
                FormatLike(libraryGroup.Elements(TcNs + "LibraryReference").FirstOrDefault()
                    ?? referencesGroup?.Elements(TcNs + "PlaceholderReference").FirstOrDefault(), library);
                AppendPlcItem(referencesGroup, library);
            }
            else
            {
                if (resolutionsGroup == null)
                {
                    resolutionsGroup = new XElement(TcNs + "ItemGroup");
                    AppendPlcItem(project, resolutionsGroup);
                }

                if (referencesGroup == null)
                {
                    referencesGroup = new XElement(TcNs + "ItemGroup");
                    AppendPlcItem(project, referencesGroup);
                }

                var reference = new XElement(TcNs + "PlaceholderReference",
                        new XAttribute("Include", package.PackageVersion.Title),
                        new List<XElement> {
                            new XElement(TcNs + "DefaultResolution", $"{package.PackageVersion.Title}, {(TwinCATLibraryVersion(package.Config.Version) ?? "*")} ({distributorName})"),
                            new XElement(TcNs + "Namespace", ns),
                        }
                    );

                AddOptions(reference, package.Config?.Options);
                AddParameters(reference, package.Config?.Parameters);
                foreach (var child in preservedChildren)
                    reference.Add(new XElement(child));
                FormatLike(referencesGroup.Elements(TcNs + "PlaceholderReference").FirstOrDefault(), reference);
                AppendPlcItem(referencesGroup, reference);

                var resolution = new XElement(TcNs + "PlaceholderResolution",
                        new XAttribute("Include", package.PackageVersion.Title),
                        new XElement(TcNs + "Resolution", $"{package.PackageVersion.Title}, {(TwinCATLibraryVersion(package.PackageVersion.Version) ?? "*")} ({distributorName})")
                     );
                FormatLike(resolutionsGroup.Elements(TcNs + "PlaceholderResolution").FirstOrDefault(), resolution);
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
                element.Remove();

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "PlaceholderReference")?.Any() == true)?.FirstOrDefault()) != null)
                element.Remove();

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(x => x.Elements(TcNs + "LibraryReference")?.Any() == true)?.FirstOrDefault()) != null)
                element.Remove();

            while ((element = project.Elements(TcNs + "ItemGroup")?.Where(e => !e.HasElements).FirstOrDefault()) != null)
                element.Remove();

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
                        propertyGroup.Add(new XElement(TcNs + "Name", plc.Name));

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
                        propertyGroup.Add(new XElement(TcNs + "Title", titleStr));

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
                        propertyGroup.Add(new XElement(TcNs + "Company", plc.DistributorName));

                    _logger.Info("[plc] updated company to {0}", plc.DistributorName);
                }
                else if((plc.PlcType == ConfigPlcProject.PlcProjectType.Library || plc.PlcType == ConfigPlcProject.PlcProjectType.Library) && string.IsNullOrEmpty(plc.DistributorName))
                {
                    throw new ArgumentException("Distributor name is empty, but it is mandatory for libraries!");
                }
                else
                {
                    var fallbackCompany = "Unknown Company";
                    if (company != null)
                        company.Value = fallbackCompany;
                    else
                        propertyGroup.Add(new XElement(TcNs + "Company", fallbackCompany));

                    _logger.Info("[plc] updated company to {0}", plc.DistributorName);
                }

                var versionStr = TwincatNumericVersion(NormalizedVersion(plc.Version));
                if (!string.IsNullOrEmpty(versionStr))
                {
                    if (projectVersion != null)
                        projectVersion.Value = versionStr;
                    else
                        propertyGroup.Add(new XElement(TcNs + "ProjectVersion", versionStr));

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

            return existing.Elements()
                .Where(element => !ManagedReferenceChildren.Contains(element.Name.LocalName))
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

        // A loaded plcproj stores its indentation as whitespace text nodes. New elements are inserted
        // with the same indent as their siblings, ahead of the closing tag's whitespace.
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

            if (indent != null && hasTrailing)
            {
                trailing.AddBeforeSelf(new XText(indent.Value));
                trailing.AddBeforeSelf(child);
                return;
            }

            if (indent != null)
            {
                parent.Add(new XText(indent.Value));
                parent.Add(child);
                return;
            }

            parent.Add(child);
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
            var children = created.Elements().ToList();
            foreach (var child in children)
                child.Remove();

            foreach (var child in children)
            {
                created.Add(new XText(innerIndent.Value));
                created.Add(child);
            }

            if (closing != null && string.IsNullOrWhiteSpace(closing.Value))
                created.Add(new XText(closing.Value));
        }

        public override void SaveAsLibrary(ConfigPlcProject plc, string filePath) { }
    }
}
