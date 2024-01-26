using EnvDTE;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Models;
using EnvDTE80;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.Threading;
using Microsoft.VisualStudio.Threading;

namespace Twinpack
{
    public class TwinpackUtils
    {
        private static readonly Guid _libraryManagerGuid = Guid.Parse("e1825adc-a79c-4e8e-8793-08d62d84be5b");
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        public static string TwincatPath
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

        public static string LicensesPath = TwincatPath + @"\CustomConfig\Licenses";
        public static string BootFolderPath = TwincatPath + @"\Boot";

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static ITcSysManager SystemManager(Solution solution, ConfigPlcProject plcConfig)
        {
            foreach (Project prj in solution.Projects)
            {
                ITcSysManager2 systemManager = prj.Object as ITcSysManager2;
                var project = new ConfigProject();
                project.Name = prj.Name;

                ITcSmTreeItem plcs = systemManager.LookupTreeItem("TIPC");
                foreach (ITcSmTreeItem9 plc in plcs)
                {
                    if (plc is ITcProjectRoot)
                    {
                        string xml = plc.ProduceXml();
                        string projectPath = XElement.Parse(xml).Element("PlcProjectDef").Element("ProjectPath").Value;
                        if (Path.GetFileNameWithoutExtension(projectPath) == plcConfig.Name && prj.Name == plcConfig.ProjectName)
                            return systemManager;

                    }
                }
            }

            return null;
        }

        public static Project ActivePlc(DTE2 dte)
        {
            if (dte?.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects?.Length > 0)
            {
                var prj = activeSolutionProjects?.GetValue(0) as Project;
                try
                {
                    ITcSysManager2 systemManager = (prj.Object as dynamic).SystemManager as ITcSysManager2;
                    if (systemManager != null)
                        return prj;
                }
                catch { }
            }

            return null;
        }

        public static void CloseAllPackageRelatedWindows(DTE2 dte, PackageVersionGetResponse packageVersion)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            // Close all windows that have been opened with a library manager
            foreach (Window window in dte.Windows)
            {
                try
                {
                    var obj = (window as dynamic).Object;
                    if (obj == null)
                        continue;

                    if (obj.EditorViewFactoryGuid == _libraryManagerGuid)
                    {
                        window.Close(vsSaveChanges.vsSaveChangesNo);
                        continue;
                    }

                    if (!(obj.FileName as string).StartsWith("["))
                        continue;

                    if (Regex.Match(obj.FileName, $@"\[{packageVersion.Name}.*?\({packageVersion.DistributorName}\)\].*", RegexOptions.IgnoreCase).Success)
                    {
                        window.Close(vsSaveChanges.vsSaveChangesNo);
                        continue;
                    }

                    foreach (var dependency in packageVersion.Dependencies ?? new List<PackageVersionGetResponse>())
                    {
                        if (Regex.Match(obj.FileName, $@"\[{dependency.Name}.*?\({dependency.DistributorName}\)\].*", RegexOptions.IgnoreCase).Success)
                        {
                            window.Close(vsSaveChanges.vsSaveChangesNo);
                            break;
                        }
                    }

                }
                catch { }
            }
        }

        public static int BuildErrorCount(DTE2 dte)
        {
            int errorCount = 0;
            ErrorItems errors = dte.ToolWindows.ErrorList.ErrorItems;

            for (int i = 1; i <= errors.Count; i++)
            {
                var item = errors.Item(i);

                switch (item.ErrorLevel)
                {
                    case vsBuildErrorLevel.vsBuildErrorLevelHigh:
                        errorCount++;
                        break;
                    default:
                        break;
                }
            }

            return errorCount;
        }

        public static void SyncPlcProj(ITcPlcIECProject2 plc, ConfigPlcProject plcConfig)
        {
            StringWriter stringWriter = new StringWriter();
            using (XmlWriter writer = XmlTextWriter.Create(stringWriter))
            {
                _logger.Info($"Updating plcproj, setting Version={plcConfig.Version}, Company={plcConfig.DistributorName}");
                writer.WriteStartElement("TreeItem");
                writer.WriteStartElement("IECProjectDef");
                writer.WriteStartElement("ProjectInfo");
                writer.WriteElementString("Title", plcConfig.Title);
                writer.WriteElementString("Version", (new Version(plcConfig.Version)).ToString());
                writer.WriteElementString("Company", plcConfig.DistributorName);
                writer.WriteEndElement();     // ProjectInfo
                writer.WriteEndElement();     // IECProjectDef
                writer.WriteEndElement();     // TreeItem 
            }
            (plc as ITcSmTreeItem).ConsumeXml(stringWriter.ToString());
        }

        public static void UninstallReferenceAsync(ITcPlcLibraryManager libManager, PackageVersionGetResponse packageVersion, CancellationToken cancellationToken = default)
        {
            libManager.UninstallLibrary("System", packageVersion.Title, packageVersion.Version, packageVersion.DistributorName);
            foreach (var dependency in packageVersion.Dependencies ?? new List<PackageVersionGetResponse>())
            {
                libManager.UninstallLibrary("System", dependency.Title, dependency.Version, dependency.DistributorName);
            }
        }

        public static bool IsPackageInstalled(ITcPlcLibraryManager libManager, PackageGetResponse package)
        {
            foreach (ITcPlcLibrary r in libManager.ScanLibraries())
            {
                if (string.Equals(r.Name, package.Title, StringComparison.InvariantCultureIgnoreCase) &&
                    string.Equals(r.Distributor, package.DistributorName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static async Task<List<PackageVersionGetResponse>> DownloadPackageVersionAndDependenciesAsync(ITcPlcLibraryManager libManager, PackageVersionGetResponse packageVersion, TwinpackServer server, List<PackageVersionGetResponse> downloadedPackageVersions, bool forceDownload = true, string cachePath = null, CancellationToken cancellationToken = default)
        {
            // check if we find the package on the system
            bool referenceFound = false;
            if (!forceDownload && libManager != null)
            {
                foreach (ITcPlcLibrary r in libManager.ScanLibraries())
                {
                    if (string.Equals(r.Name, packageVersion.Title, StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(r.Distributor, packageVersion.DistributorName, StringComparison.InvariantCultureIgnoreCase) &&
                        r.Version == packageVersion.Version)
                    {
                        referenceFound = true;
                        break;
                    }
                }

                if (referenceFound)
                {
                    _logger.Info($"Skipping download for {packageVersion.Title} (version: {packageVersion.Version}, distributor: {packageVersion.DistributorName}), it already exists on the system");
                }
            }

            if ((!referenceFound || forceDownload) && downloadedPackageVersions.Any(x => x.PackageVersionId == packageVersion.PackageVersionId) == false)
            {
                await server.DownloadPackageVersionAsync(packageVersion, checksumMode: TwinpackServer.ChecksumMode.IgnoreMismatch, cachePath: cachePath, cancellationToken: cancellationToken);
                downloadedPackageVersions.Add(packageVersion);
            }

            foreach (var dependency in packageVersion?.Dependencies ?? new List<PackageVersionGetResponse>())
            {
                downloadedPackageVersions = await DownloadPackageVersionAndDependenciesAsync(libManager, dependency, server, downloadedPackageVersions, forceDownload, cachePath, cancellationToken: cancellationToken);
            }

            return downloadedPackageVersions;
        }

        public static void InstallPackageVersions(ITcPlcLibraryManager libManager, List<PackageVersionGetResponse> packageVersions, string cachePath = null)
        {

            foreach (var packageVersion in packageVersions)
            {
                _logger.Info($"Installing package {packageVersion.Name} ...");

                var suffix = packageVersion.Compiled == 1 ? "compiled-library" : "library";
                libManager.InstallLibrary("System", Path.GetFullPath($@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}\{packageVersion.Name}_{packageVersion.Version}.{suffix}"), bOverwrite: true);
            }
        }

        public static string GuessDistributorName(ITcPlcLibraryManager libManager, string libraryName, string version)
        {
            // try to find the vendor
            foreach (ITcPlcLibrary r in libManager.ScanLibraries())
            {
                if (r.Name == libraryName && (r.Version == version || version == "*"))
                {
                    return r.Distributor;
                }
            }

            return null;
        }

        public static void RemoveReference(ITcPlcLibraryManager libManager, string placeholderName, string libraryName, string version, string distributorName)
        {
            distributorName = distributorName ?? GuessDistributorName(libManager, libraryName, version);

            // Try to remove the already existing reference
            foreach (ITcPlcLibRef item in libManager.References)
            {
                string itemPlaceholderName;
                string itemDistributorName;
                string itemVersion;

                try
                {
                    ITcPlcPlaceholderRef2 plcPlaceholder; // this will through if the library is currently not installed
                    ITcPlcLibrary plcLibrary;
                    plcPlaceholder = (ITcPlcPlaceholderRef2)item;

                    itemPlaceholderName = plcPlaceholder.PlaceholderName;

                    if (plcPlaceholder.EffectiveResolution != null)
                        plcLibrary = plcPlaceholder.EffectiveResolution;
                    else
                        plcLibrary = plcPlaceholder.DefaultResolution;

                    itemVersion = plcLibrary.Version;
                    itemDistributorName = plcLibrary.Distributor;
                }
                catch
                {
                    ITcPlcLibrary plcLibrary;
                    plcLibrary = (ITcPlcLibrary)item;
                    itemVersion = "Unknown";
                    itemPlaceholderName = plcLibrary.Name.Split(',')[0];
                    itemDistributorName = plcLibrary.Distributor;
                }

                if (string.Equals(itemPlaceholderName, placeholderName, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Info($"Remove reference {placeholderName} (version: {itemVersion}, distributor: {itemDistributorName})");
                    libManager.RemoveReference(placeholderName);
                }
            }
        }

        public static void AddReferences(ITcPlcLibraryManager libManager, IEnumerable<PackageVersionGetResponse> packageVersions, bool addDependenciesAsReferences)
        {
            if(addDependenciesAsReferences)
            {
                packageVersions = packageVersions.Concat(packageVersions.SelectMany(x => x.Dependencies));
            }

            foreach (var packageVersion in packageVersions.Distinct())
            {
                AddReference(libManager, packageVersion.Title, packageVersion.Title, packageVersion.Version, packageVersion.DistributorName);
            }
        }
        public static void AddReference(ITcPlcLibraryManager libManager, string placeholderName, string libraryName, string version, string distributorName, bool addAsPlaceholder = true)
        {
            distributorName = distributorName ?? GuessDistributorName(libManager, libraryName, version);
            RemoveReference(libManager, placeholderName, libraryName, version, distributorName);

            _logger.Info($"Adding reference to {placeholderName} (version: {version}, distributor: {distributorName})");
            if (addAsPlaceholder)
                libManager.AddPlaceholder(placeholderName, libraryName, version, distributorName);
            else
                libManager.AddLibrary(libraryName, version, distributorName);
        }

        public static string ParseLicenseId(string content)
        {
            try
            {
                var xdoc = XDocument.Parse(content);
                return xdoc.Elements("TcModuleClass")?.Elements("Licenses")?.Elements("License")?.Elements("LicenseId")?.FirstOrDefault()?.Value;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex);
            }

            return null;
        }

        public static HashSet<string> CopyLicenseTmcIfNeeded(IEnumerable<PackageVersionGetResponse> packageVersions, HashSet<string> knownLicenseIds)
        {
            // todo: flatten dependences and package versions and iterate over this
            foreach(var packageVersion in packageVersions)
            {
                if (packageVersion.HasLicenseTmcBinary)
                {
                    _logger.Trace($"Copying license description file to TwinCAT for {packageVersion.Name} ...");
                    try
                    {
                        var licenseId = ParseLicenseId(packageVersion.LicenseTmcText);
                        if (licenseId == null)
                            throw new InvalidDataException("The tmc file is not a valid license file!");

                        if (knownLicenseIds.Contains(licenseId))
                        {
                            _logger.Trace($"LicenseId={licenseId} already known");
                        }
                        else
                        {
                            _logger.Info($"Copying license tmc with licenseId={licenseId} to {LicensesPath}");

                            using (var md5 = MD5.Create())
                            {
                                if (!Directory.Exists(LicensesPath))
                                    Directory.CreateDirectory(LicensesPath);

                                File.WriteAllText(Path.Combine(LicensesPath, BitConverter.ToString(md5.ComputeHash(Encoding.ASCII.GetBytes($"{packageVersion.DistributorName}{packageVersion.Name}"))).Replace("-", "") + ".tmc"),
                                                  packageVersion.LicenseTmcText);

                            }

                            knownLicenseIds.Add(licenseId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex.Message);
                        _logger.Trace(ex);
                    }
                }

                if(packageVersion.Dependencies != null)
                {
                    knownLicenseIds = CopyLicenseTmcIfNeeded(packageVersion.Dependencies, knownLicenseIds);
                }
            }

            return knownLicenseIds;
        }

        public static HashSet<string> KnownLicenseIds()
        {
            var result = new HashSet<string>();
            if (!Directory.Exists(LicensesPath))
                return result;

            foreach (var fileName in Directory.GetFiles(LicensesPath, "*.tmc", SearchOption.AllDirectories))
            {
                try
                {
                    var licenseId = ParseLicenseId(File.ReadAllText(fileName));

                    if (licenseId == null)
                        throw new InvalidDataException("The file {fileName} is not a valid license file!");

                    result.Add(licenseId);
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex);
                }


            }

            return result;
        }

        public static IEnumerable<ConfigPlcProject> PlcProjectsFromConfig(bool compiled, string target, string rootPath = ".", string cachePath = null)
        {
            var config = ConfigFactory.Load(rootPath);

            _logger.Info($"Pushing to Twinpack Server");

            var suffix = compiled ? "compiled-library" : "library";
            var plcs = config.Projects.SelectMany(x => x.Plcs)
                                      .Where(x => x.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary ||
                                             x.PlcType == ConfigPlcProject.PlcProjectType.Library);
            // check if all requested files are present
            foreach (var plc in plcs)
            {
                plc.FilePath = $@"{cachePath ?? DefaultLibraryCachePath}\{target}\{plc.Name}_{plc.Version}.{suffix}";
                if (!File.Exists(plc.FilePath))
                    throw new LibraryNotFoundException(plc.Name, plc.Version, $"Could not find library file '{plc.FilePath}'");

                if (!string.IsNullOrEmpty(plc.LicenseFile) && !File.Exists(plc.LicenseFile))
                    _logger.Warn($"Could not find license file '{plc.LicenseFile}'");

                yield return plc;
            }
        }

        public static IEnumerable<ConfigPlcProject> PlcProjectsFromPath(string rootPath = ".")
        {
            foreach (var libraryFile in Directory.GetFiles(rootPath, "*.library"))
            {
                var libraryInfo = LibraryReader.Read(File.ReadAllBytes(libraryFile));
                var plc = new ConfigPlcProject()
                {
                    Name = libraryInfo.Title,
                    DisplayName = libraryInfo.Title,
                    Description = libraryInfo.Description,
                    Authors = libraryInfo.Author,
                    DistributorName = libraryInfo.Company,
                    Version = libraryInfo.Version,
                    FilePath = libraryFile
                };

                yield return plc;
            }
        }
    }
}
