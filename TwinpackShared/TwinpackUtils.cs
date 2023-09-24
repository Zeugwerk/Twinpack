using EnvDTE;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using TCatSysManagerLib;
using Twinpack.Models;
using Twinpack.Exceptions;
using EnvDTE80;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;

namespace Twinpack
{
    public class TwinpackUtils
    {
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        public static string LicensesPath = @"C:\TwinCAT\3.1\CustomConfig\Licenses";

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
                    if(systemManager != null)
                        return prj;
                }
                catch {}
            }

            return null;
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

        public static void UninstallReferenceAsync(ITcPlcLibraryManager libManager, PackageVersionGetResponse packageVersion)
        {
            libManager.UninstallLibrary("System", packageVersion.Title, packageVersion.Version, packageVersion.DistributorName);
            foreach(var dependency in packageVersion.Dependencies)
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

        public static async Task<List<PackageVersionGetResponse>> DownloadPackageVersionAndDependenciesAsync(ITcPlcLibraryManager libManager, PackageVersionGetResponse packageVersion, TwinpackServer server, bool forceDownload = true, string cachePath = null)
        {
            var downloadedPackageVersions = new List<PackageVersionGetResponse> { };

            // check if we find the package on the system
            bool referenceFound = false;
            if (!forceDownload)
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

            if (!referenceFound || forceDownload)
            {
                _logger.Info($"Downloading {packageVersion.Title} (version: {packageVersion.Version}, distributor: {packageVersion.DistributorName})");

                downloadedPackageVersions.Add(await server.GetPackageVersionAsync((int)packageVersion.PackageVersionId,
                                    includeBinary: true, cachePath: cachePath));
            }

            foreach (var dependency in packageVersion?.Dependencies ?? new List<PackageVersionGetResponse>())
            {
                downloadedPackageVersions.AddRange(await DownloadPackageVersionAndDependenciesAsync(libManager, dependency, server, forceDownload, cachePath));
            }

            return downloadedPackageVersions;
        }

        public static async Task InstallPackageVersionsAsync(ITcPlcLibraryManager libManager, List<PackageVersionGetResponse> packageVersions, string cachePath = null)
        {
            foreach(var packageVersion in packageVersions)
            {
                var suffix = packageVersion.Compiled == 1 ? "compiled-library" : "library";

                await Task.Run( () => { libManager.InstallLibrary("System", $@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}\{packageVersion.Name}_{packageVersion.Version}.{suffix}", bOverwrite: true);  });
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

                if (string.Equals(itemPlaceholderName, placeholderName, StringComparison.InvariantCultureIgnoreCase) &&
                    string.Equals(itemDistributorName, distributorName, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Info($"Remove reference to {placeholderName} (version: {itemVersion}, distributor: {itemDistributorName})");
                    libManager.RemoveReference(placeholderName);
                }
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
            catch(Exception ex)
            {
                _logger.Trace(ex);
            }

            return null;
        }

        public static void CopyLicenseTmcIfNeeded(PackageVersionGetResponse packageVersion, HashSet<string> knownLicenseIds)
        {
            if (!packageVersion.HasLicenseTmcBinary)
                return;

            try
            {
                var licenseId = ParseLicenseId(packageVersion.LicenseTmcText);
                if(licenseId == null)
                    throw new InvalidDataException("The tmc file is not a valid license file!");

                if (knownLicenseIds.Contains(licenseId))
                {
                    _logger.Info($"LicenseId={licenseId} already known");
                    return;
                }
                else
                {
                    _logger.Info($"Copying license tmc with licenseId={licenseId} to {LicensesPath}");

                    using(var md5 = MD5.Create())
                    {
                        if(!Directory.Exists(LicensesPath))
                            Directory.CreateDirectory(LicensesPath);

                        File.WriteAllText(Path.Combine(LicensesPath, BitConverter.ToString(md5.ComputeHash(Convert.FromBase64String($"{packageVersion.DistributorName}{packageVersion.Name}"))).Replace("-", "") + ".tmc"),
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
    }
}
