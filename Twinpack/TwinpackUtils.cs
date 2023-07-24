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

namespace Twinpack
{
    public class TwinpackUtils
    {
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        static public ITcSysManager SystemManager(Solution solution, ConfigPlcProject plcConfig)
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

        static public int BuildErrorCount(DTE2 dte)
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

        static public void SyncPlcProj(ITcPlcIECProject2 plc, ConfigPlcProject plcConfig)
        {
            StringWriter stringWriter = new StringWriter();
            using (XmlWriter writer = XmlTextWriter.Create(stringWriter))
            {
                _logger.Info($"Updating plcproj file with Version={plcConfig.Version}, Company={plcConfig.DistributorName}");
                writer.WriteStartElement("TreeItem");
                writer.WriteStartElement("IECProjectDef");
                writer.WriteStartElement("ProjectInfo");
                writer.WriteElementString("Version", (new Version(plcConfig.Version)).ToString());
                writer.WriteElementString("Company", plcConfig.DistributorName);
                writer.WriteEndElement();     // ProjectInfo
                writer.WriteEndElement();     // IECProjectDef
                writer.WriteEndElement();     // TreeItem 
            }
            (plc as ITcSmTreeItem).ConsumeXml(stringWriter.ToString());
        }

        static public void UninstallReferenceAsync(ITcPlcLibraryManager libManager, PackageVersionGetResponse packageVersion)
        {
            libManager.UninstallLibrary("System", packageVersion.Name, packageVersion.Version, packageVersion.DistributorName);
            foreach(var dependency in packageVersion.Dependencies)
            {
                libManager.UninstallLibrary("System", dependency.Name, dependency.Version, dependency.DistributorName);
            }
        }

        public static bool IsPackageInstalled(ITcPlcLibraryManager libManager, PackageGetResponse package)
        {
            foreach (ITcPlcLibrary r in libManager.ScanLibraries())
            {
                if (r.Name == package.Name && r.Distributor == package.DistributorName)
                {
                    return true;
                }
            }

            return false;
        }
            
        static public async Task InstallReferenceAsync(ITcPlcLibraryManager libManager, PackageVersionGetResponse packageVersion, TwinpackServer server, bool forceDownload = true, string cachePath = null)
        {
            // check if we find the package on the system
            bool referenceFound = false;
            if(!forceDownload)
            {
                foreach (ITcPlcLibrary r in libManager.ScanLibraries())
                {
                    if (r.Name == packageVersion.Name && r.Version == packageVersion.Version && r.Distributor == packageVersion.DistributorName)
                    {
                        referenceFound = true;
                        break;
                    }
                }

                if (referenceFound)
                {
                    _logger.Info($"The package {packageVersion.Name}  (version: {packageVersion.Version}, distributor: {packageVersion.DistributorName}) already exists on the system");
                }
            }


            if (!referenceFound || forceDownload)
            {
                _logger.Info($"Downloading {packageVersion.Name}  (version: {packageVersion.Version}, distributor: {packageVersion.DistributorName})");

                packageVersion = await server.GetPackageVersionAsync((int)packageVersion.PackageVersionId,
                                    includeBinary: true, cachePath: cachePath);

                _logger.Info($"Installing {packageVersion.Name}, {packageVersion.Version}, {packageVersion.DistributorName}");
                var suffix = packageVersion.Compiled == 1 ? "compiled-library" : "library";
                libManager.InstallLibrary("System", $@"{cachePath ?? DefaultLibraryCachePath}\{packageVersion.Target}\{packageVersion.Name}_{packageVersion.Version}.{suffix}", bOverwrite: true);
            }

            foreach(var dependency in packageVersion.Dependencies)
            {
                await InstallReferenceAsync(libManager, dependency, server, forceDownload, cachePath);
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

                    itemDistributorName = plcLibrary.Distributor;
                }
                catch
                {
                    ITcPlcLibrary plcLibrary;
                    plcLibrary = (ITcPlcLibrary)item;
                    itemPlaceholderName = plcLibrary.Name.Split(',')[0];
                    itemDistributorName = plcLibrary.Distributor;
                }

                if (string.Equals(itemPlaceholderName, placeholderName, StringComparison.InvariantCultureIgnoreCase) &&
                    string.Equals(itemDistributorName, distributorName, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.Info("Remove reference to {placeholderName} from PLC");
                    libManager.RemoveReference(placeholderName);
                }
            }
        }

        public static void AddReference(ITcPlcLibraryManager libManager, string placeholderName, string libraryName, string version, string distributorName, bool addAsPlaceholder = true)
        {
            distributorName = distributorName ?? GuessDistributorName(libManager, libraryName, version);
            RemoveReference(libManager, placeholderName, libraryName, version, distributorName);

            _logger.Info("Adding reference to {placeholderName}  (version: {version}, distributor: {distributorName}) to PLC");
            if (addAsPlaceholder)
                libManager.AddPlaceholder(placeholderName, libraryName, version, distributorName);
            else
                libManager.AddLibrary(libraryName, version, distributorName);
        }

        static public BitmapImage IconImage(string iconUrl)
        {
            if (iconUrl == null)
                return null;

            try
            {
                BitmapImage img = new BitmapImage();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.BeginInit();
                img.UriSource = new Uri(iconUrl, UriKind.Absolute);
                img.EndInit();
                return img;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
