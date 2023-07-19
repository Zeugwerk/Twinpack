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
        public class RetrieveVersionResult
        {
            public RetrieveVersionResult()
            {
                Success = false;
            }

            public bool Success { get; set; }
            public string ActualVersion { get; set; }
        }

        public static string DefaultLibraryCachePath { get { return $@"{Directory.GetCurrentDirectory()}\.Zeugwerk\libraries"; } }
        public static string DefaultUsername = "public";
        public static string DefaultPassword = "public";
        public static bool UseMainBranch = true;
        public static HashSet<PlcLibrary> InstalledLibraries { get; set; } = new HashSet<PlcLibrary>();

        private static bool Informed = false;
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        static public ITcSysManager SystemManager(Solution solution, Models.ConfigProject plc)
        {

            return null;
        }

        static public HttpRequestMessage CreateHttpRequest(Uri uri, HttpMethod method, bool authorize = true)
        {
            // Set Credentials for download
            String username = Environment.GetEnvironmentVariable("ZGWK_USERNAME") ?? DefaultUsername;
            String password = Environment.GetEnvironmentVariable("ZGWK_PASSWORD") ?? DefaultPassword;
            if (!Informed)
            {
                if (username == DefaultUsername && password == DefaultPassword)
                {
                    Informed = true;
                    _logger.Debug(" Using public credentials, access is limited to public areas. " +
                                      "Set ZGWK_USERNAME and ZGWK_PASSWORD environment variables to access restricted areas!");
                }
                else
                {
                    Informed = true;
                    _logger.Debug($"Using {username}/*** credentials");
                }
            }

            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));

            // create Request
            HttpRequestMessage request = new HttpRequestMessage(method, uri);
            if (authorize)
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            return request;
        }

        static public async Task<bool> IsUriAvailableAsync(Uri uri, bool authorize = true)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var request = CreateHttpRequest(uri, HttpMethod.Head, authorize);
                    var response = await client.SendAsync(request);
                    var headers = response.Content.Headers;
                    if (response.StatusCode == System.Net.HttpStatusCode.OK && (headers.ContentType == null || headers.ContentType.MediaType != "text/html"))
                    {
                        _logger.Trace($"Uri IS     available ({uri})");
                        return true;
                    }

                    _logger.Trace($"Uri IS NOT available ({uri})");
                }
                catch
                {
                    _logger.Trace($"Uri IS NOT available ({uri})");
                }

                return false;
            }
        }

        public static string FindTwincatSubfolder(string used_tcversion, string targetPath, List<string> supported_tcversions = null)
        {
            var used = used_tcversion.Split('.');
            if (supported_tcversions != null)
                while (used.Length > 0 && !supported_tcversions.Exists(x => string.Join(".", used).ToLower() == x.ToLower()))
                    Array.Resize(ref used, used.Length - 1);
            else
                while (used.Length > 0 && !Directory.Exists($@"{targetPath}\{string.Join(".", used).ToLower()}"))
                    Array.Resize(ref used, used.Length - 1);

            if (used.Length == 0)
                return null;

            return string.Join(".", used);
        }

        public static string FindLibraryFilePathWithoutExtension(string tcversion, string referencename, string referenceversion, string targetPath)
        {
            targetPath = targetPath ?? TwinpackUtils.DefaultLibraryCachePath;

            if (referenceversion.Split('.').Length == 4)
                return string.Join("\\", new string[] { targetPath, $"{TwinpackUtils.FindTwincatSubfolder(tcversion, targetPath)}", $"{referencename}_{referenceversion}." });

            return Directory.GetFiles(string.Join("\\", new string[] { targetPath, $"{TwinpackUtils.FindTwincatSubfolder(tcversion, targetPath)}" }), $"{referencename}_*library", SearchOption.TopDirectoryOnly)
                .Select(x => new FileInfo(x))
                .OrderBy(x => x.CreationTime)
                .Select(x => Path.ChangeExtension(x.FullName, ""))
                .FirstOrDefault();
        }

        static public bool IsCached(string used_tcversion, List<string> references, string version, string cachePath)
        {
            List<string> paths = new List<string>();
            if (version == null)
                paths = references.Select(x => $@"{used_tcversion}\{x.Split('=').First()}_{x.Split('=').Last()}").ToList();
            else
                paths = references.Select(x => $@"{used_tcversion}\{x}_{version}").ToList();

            foreach (var filename in paths)
            {
                if (!File.Exists($@"{cachePath}\{filename}.compiled-library") &&
                    !File.Exists($@"{cachePath}\{filename}.library"))
                {
                    _logger.Info($"One or more references for TwinCAT {used_tcversion} not found in cache!");
                    return false;
                }
            }

            return true;
        }

        static public int BuildErrorCount(EnvDTE80.DTE2 dte)
        {
            int errorCount = 0;
            EnvDTE80.ErrorItems errors = dte.ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= errors.Count; i++)
            {
                var item = errors.Item(i);

                switch (item.ErrorLevel)
                {
                    case EnvDTE80.vsBuildErrorLevel.vsBuildErrorLevelHigh:
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

        static public void AddReference(ITcPlcLibraryManager libManager, string placeholderName, string libraryName, string version, string vendor, bool addAsPlaceholder = true)
        {
            // try to find the vendor
            if(vendor == null)
            {
                _logger.Warn($"Trying to add a reference {libraryName}={version} without an explicit vendor");
                foreach (ITcPlcLibrary r in libManager.ScanLibraries())
                {
                    if (r.Name == libraryName && (r.Version == version || version == "*"))
                    {
                        vendor = r.Distributor;
                        break;
                    }
                }
                _logger.Warn($"Guessed vendor of {libraryName}={version} with {vendor}");
            }


            // Try to remove the already existing reference
            foreach (ITcPlcLibRef item in libManager.References)
            {
                string libName;
                string itemPlaceholderName;
                string distributor;
                string displayName;

                try
                {
                    ITcPlcPlaceholderRef2 plcPlaceholder;
                    ITcPlcLibrary plcLibrary;
                    plcPlaceholder = (ITcPlcPlaceholderRef2)item;

                    itemPlaceholderName = plcPlaceholder.PlaceholderName;

                    if (plcPlaceholder.EffectiveResolution != null)
                        plcLibrary = (ITcPlcLibrary)plcPlaceholder.EffectiveResolution;
                    else
                        plcLibrary = (ITcPlcLibrary)plcPlaceholder.DefaultResolution;

                    libName = plcLibrary.Name.Split(',')[0];
                    distributor = plcLibrary.Distributor;
                    displayName = plcLibrary.DisplayName;
                }
                catch
                {
                    ITcPlcLibrary plcLibrary;
                    plcLibrary = (ITcPlcLibrary)item;
                    libName = plcLibrary.Name.Split(',')[0];
                    distributor = plcLibrary.Distributor;
                    displayName = plcLibrary.DisplayName;
                    itemPlaceholderName = libName;
                }

                if (string.Equals(itemPlaceholderName, placeholderName, StringComparison.InvariantCultureIgnoreCase))
                    libManager.RemoveReference(placeholderName);
            }

            if (addAsPlaceholder)
                libManager.AddPlaceholder(placeholderName, libraryName, version, vendor);
            else
                libManager.AddLibrary(libraryName, version, vendor);
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
