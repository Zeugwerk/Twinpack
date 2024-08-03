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

namespace Twinpack
{
    public class TwinpackUtils
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }


        public static EnvDTE.Project ActiveProject(DTE2 dte)
        {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread

            if (dte?.ActiveSolutionProjects is Array activeSolutionProjects && activeSolutionProjects?.Length > 0)
            {
                var prj = activeSolutionProjects?.GetValue(0) as EnvDTE.Project;
                try
                {
                    ITcSysManager2 systemManager = (prj.Object as dynamic).SystemManager as ITcSysManager2;
                    if (systemManager != null)
                        return prj;
                }
                catch { }
            }

#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

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


        public static bool IsPackageInstalled(ITcPlcLibraryManager libManager, string distributorName, string title)
        {
            foreach (ITcPlcLibrary r in libManager.ScanLibraries())
            {
                if (string.Equals(r.Name, title, StringComparison.InvariantCultureIgnoreCase) &&
                    string.Equals(r.Distributor, distributorName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
                    throw new Exceptions.LibraryNotFoundException(plc.Name, plc.Version, $"Could not find library file '{plc.FilePath}'");

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
