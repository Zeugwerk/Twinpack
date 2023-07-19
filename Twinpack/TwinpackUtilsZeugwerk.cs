using EnvDTE;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TCatSysManagerLib;

namespace Twinpack
{
    public class TwinpackUtilsZeugwerk : TwinpackUtils
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        static private async Task<Stream> DownloadAsync(Uri archiveUri, bool authorize = true)
        {
            using (HttpClient client = new HttpClient())
            {
                _logger.Info($"Downloading {archiveUri}");
                HttpRequestMessage request = CreateHttpRequest(archiveUri, HttpMethod.Get, authorize);
                var response = await client.SendAsync(request);
                return await response.Content.ReadAsStreamAsync();
            }
        }

        static private async Task<bool> DownloadAndExtractDocFromArchiveAsync(string repository, string version, string targetPath)
        {
            // download doc as archive - if available
            var branches = PossibleBranches(version);
            var archiveUris = branches.Select(x => new Uri($"{repository}/{x}/Zeugwerk_Framework-doc-{version}.zip")).ToList();
            archiveUris.Add(new Uri($"{repository}/Zeugwerk_Framework-{version}.zip")); // fallback to explicit branch with same version

            foreach (Uri uri in archiveUris)
            {
                Stream stream = null;
                if (await IsUriAvailableAsync(uri) &&
                    (stream = await DownloadAsync(uri)) != null)
                {

                    ZipArchive archive = null;

                    // check if downloaded file is a valid zip archive -> if exception thrown it is not
                    try
                    {
                        archive = new ZipArchive(stream);
                    }
                    catch (Exception e)
                    {
                        archive = null;
                        _logger.Trace($"{e.Message}");
                    }

                    if (archive != null)
                    {
                        string unzipDirectory = Path.Combine(targetPath, "documentation");
                        Extensions.DirectoryExtension.RecreateDirectory(unzipDirectory);
                        archive.ExtractToDirectory(targetPath);

                        return true;
                    }
                }
            }

            return false;
        }

        static private async Task<RetrieveVersionResult> DownloadAndExtractFromArchiveAsync(string used_tcversion, string repository, List<string> references, string version, string targetPath)
        {
            RetrieveVersionResult result = new RetrieveVersionResult();

            // download framework as archive - if available, otherwise we fall back to seperate files later on
            var branches = PossibleBranches(version);
            var targetVersion = version.Split('.').Length == 4 ? version : "latest";
            var archiveUris = branches.Select(x => new Uri($"{repository}/{x}/Zeugwerk_Framework-{targetVersion}.zip")).ToList();
            archiveUris.Add(new Uri($"{repository}/Zeugwerk_Framework-{targetVersion}.zip")); // fallback to explicit branch with same version

            result.ActualVersion = version;
            foreach (Uri uri in archiveUris)
            {
                Stream stream = null;
                if (await IsUriAvailableAsync(uri) &&
                    (stream = await DownloadAsync(uri)) != null)
                {
                    ZipArchive archive = null;

                    // check if downloaded file is a valid zip archive -> if exception thrown it is not
                    try
                    {
                        archive = new ZipArchive(stream);
                    }
                    catch (Exception e)
                    {
                        archive = null;
                        _logger.Trace($"{e.Message}");
                    }

                    if (archive != null)
                    {
                        var referenceFiles = archive.Entries.Where(x => Regex.Match(x.FullName, $@"build/.*?{used_tcversion}/.*?library", RegexOptions.IgnoreCase).Success);
                        var referencesJson = archive.Entries.Where(x => Regex.Match(x.FullName, $@"build/references.json", RegexOptions.IgnoreCase).Success).FirstOrDefault();

                        if (referencesJson != null)
                        {
                            using (var fileStream = File.Create($@"{targetPath}\references.json"))
                            {
                                await referencesJson.Open().CopyToAsync(fileStream);
                            }

                            JsonElement r = JsonSerializer.Deserialize<dynamic>(File.ReadAllText($@"{targetPath}\references.json"));
                            result.ActualVersion = r.GetProperty("version").ToString();
                        }

                        // Alle Dateien gefunden?
                        if (referenceFiles.Count() >= references.Count())
                        {
                            foreach (var rf in referenceFiles)
                            {
                                using (var fileStream = File.Create($@"{targetPath}\{used_tcversion}\{rf.Name}"))
                                {
                                    await rf.Open().CopyToAsync(fileStream);
                                }
                            }

                            result.Success = true;
                            return result;
                        }
                    }
                }
            }

            return result;
        }

        static private async Task<bool> DownloadSingleFilesAsync(string used_tcversion, string repository, List<string> references, string version, string targetPath, bool forceSingleLibs = false)
        {
            var branches = PossibleBranches(version);
            List<List<Uri>> fileCollectionUris = new List<List<Uri>>();
            IEnumerable<Tuple<string, string>> refs = new List<Tuple<string, string>>();
            if (version == null || forceSingleLibs)
                refs = references.Select(x => new Tuple<string, string>(x.Split('=').First(), x.Split('=').Last()));
            else
                refs = references.Select(x => new Tuple<string, string>(x, version));

            if (repository.EndsWith(".zip"))
            {
                fileCollectionUris.Add(new List<Uri> { new Uri(repository) });
            }
            else
            {
                foreach (var branch in branches)
                    fileCollectionUris.Add(refs.Select(x => new Uri($"{repository}/{branch}/{used_tcversion}/{x.Item1}_{x.Item2}.zip")).ToList());

                fileCollectionUris.Add(refs.Select(x => new Uri($"{repository}/{x.Item1}_{x.Item2}.zip")).ToList());

                foreach (var branch in branches)
                {
                    fileCollectionUris.Add(refs.Select(x => new Uri($"{repository}/{branch}/{used_tcversion}/{x.Item1}_{x.Item2}.compiled-library")).ToList());
                    fileCollectionUris.Add(refs.Select(x => new Uri($"{repository}/{branch}/{used_tcversion}/{x.Item1}_{x.Item2}.library")).ToList());
                }

                fileCollectionUris.Add(refs.Select(x => new Uri($"{repository}/{used_tcversion}/{x.Item1}_{x.Item2}.compiled-library")).ToList()); // fallback to explicit branch with same version
                fileCollectionUris.Add(refs.Select(x => new Uri($"{repository}/{used_tcversion}/{x.Item1}_{x.Item2}.library")).ToList()); // fallback to explicit branch with same version

                if (version == null || forceSingleLibs)
                    fileCollectionUris = fileCollectionUris.SelectMany(x => x).Select(x => new List<Uri> { x }).ToList();
            }

            IEnumerable<Uri> collection = null;
            foreach (var c in fileCollectionUris)
            {
                collection = c;
                bool authorize = true;
                foreach (var fileUri in c)
                {
                    if (await IsUriAvailableAsync(fileUri, authorize: false))
                    {
                        authorize = false;
                    }
                    else if (await IsUriAvailableAsync(fileUri, authorize: true))
                    {
                        authorize = true;
                    }
                    else
                    {
                        collection = null;
                        break;
                    }
                }

                if (collection != null)
                {
                    foreach (var fileUri in collection)
                    {
                        Stream stream = null;
                        if ((stream = await DownloadAsync(fileUri, authorize)) != null)
                        {
                            if (fileUri.AbsolutePath.ToString().EndsWith(".zip"))
                            {
                                ZipArchive archive = new ZipArchive(stream);
                                var referenceFiles = archive.Entries.Where(x => Regex.Match(x.FullName, $@".*?library", RegexOptions.IgnoreCase).Success);

                                // Alle Dateien gefunden?
                                if (referenceFiles.Count() >= references.Count() || forceSingleLibs)
                                {
                                    foreach (var rf in referenceFiles)
                                    {
                                        using (var fileStream = File.Create($@"{targetPath}\{used_tcversion}\{rf.Name}"))
                                        {
                                            await rf.Open().CopyToAsync(fileStream);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                using (var fileStream = File.Create($@"{targetPath}\{used_tcversion}\{fileUri.ToString().Split('/').Last()}"))
                                {
                                    await stream.CopyToAsync(fileStream);
                                }
                            }

                        }
                    }

                    return true;
                }
            }

            return false;
        }

        private static async Task RestorePlcDependenciesAsync(Models.ConfigPlcProject plc, string tcversion, bool force = false, string targetPath = null)
        {
            targetPath = targetPath ?? TwinpackUtils.DefaultLibraryCachePath;

            if (!Directory.Exists($@"{targetPath}\{tcversion}"))
                Directory.CreateDirectory($@"{targetPath}\{tcversion}");

            // Restore all frameworks
            foreach (var fws in plc.Frameworks)
            {
                var framework = fws.Value;
                if (framework.Repositories?.Any() == false)
                    continue;

                if (framework.References != null && framework.References.Count() > 0)
                {
                    if (force || framework.Version == "" || !TwinpackUtils.IsCached(tcversion, framework.References, framework.Version, targetPath))
                    {
                        RetrieveVersionResult result = await RestorePlcFrameworkAsync(framework.Repositories, tcversion, plc.PlcType, framework.References, framework.Version, targetPath);
                        if (!result.Success)
                        {
                            throw new DependencyNotFoundException(fws.Key, framework.Version,
                                $@"Zeugwerk Framework {framework.Version} not available and can not be restored from any given repository!");
                        }
                        framework.Version = result.ActualVersion;
                    }
                }
            }

            // Restore all single references
            if (plc.References != null)
            {
                if (plc.References.ContainsKey(tcversion))
                    await RestorePlcReferenceAsync(plc.Repositories, tcversion, plc.References[tcversion], plc.Version, targetPath);
                else if (plc.References.ContainsKey("*"))
                    await RestorePlcReferenceAsync(plc.Repositories, tcversion, plc.References["*"], plc.Version, targetPath);
            }
        }

        public static bool RestoreProject(Solution solution, Models.ConfigProject project, string tcversion, bool force = false, string targetPath = null)
        {
            ITcSysManager systemManager = SystemManager(solution, project);
            ITcSmTreeItem plcTreeItem = systemManager.LookupTreeItem("TIPC");

            // remove all plcprojects that we do not need for the configuration
            IEnumerable<string> used_plcprojects = project.Plcs.Select(plcproj => plcproj.Name);
            for (int i = 1; i <= plcTreeItem.ChildCount; ++i)
            {
                if (!used_plcprojects.Contains(plcTreeItem.Child[i].Name))
                {
                    _logger.Debug($"Removing plcproj {plcTreeItem.Child[i].Name}");
                    plcTreeItem.DeleteChild(plcTreeItem.Child[i].Name);
                }
            }

            // check if all the plcprojects we need are present
            if (plcTreeItem.ChildCount != used_plcprojects.Count())
                throw new Exception("plcprojects from configuration are missing in solution");

            // install all libraries that we actually need to proceed (according to config.json) - use the libmanager from the first plc we are using
            List<string> used_libraries = new List<string>();
            foreach (var plc in project.Plcs)
            {
                var libManager = (ITcPlcLibraryManager)systemManager.LookupTreeItem($"TIPC^{plc.Name}^{plc.Name} Project^References"); // todo: this can be done much better
                RestorePlcProject(plc, libManager, tcversion, force, targetPath);
            }

            return true;
        }

        static public void RestorePlcProject(Models.ConfigPlcProject plc, ITcPlcLibraryManager libManager, string tcversion, bool force = false, string targetPath = null)
        {
            RestorePlcDependenciesAsync(plc, tcversion, force, targetPath).Wait();

            if (!string.IsNullOrEmpty(plc?.Frameworks?.Zeugwerk?.Version))
            {
                // Add all Zeugwerk Libraries
                _logger.Debug($"Using Zeugwerk Framework {plc.Frameworks.Zeugwerk.Version}");
                foreach (string referencename in plc.Frameworks.Zeugwerk.References)
                {

                    // Install libraries if repositories were given
                    if (plc?.Frameworks?.Zeugwerk?.Repositories?.Any() == true)
                    {
                        string path = TwinpackUtils.FindLibraryFilePathWithoutExtension(tcversion, referencename, plc.Frameworks.Zeugwerk.Version, targetPath);
                        if (File.Exists(path + "compiled-library"))
                            path += "compiled-library";
                        else if (File.Exists(path + "library"))
                            path += "library";
                        else
                            throw new DependencyNotFoundException(referencename, plc.Frameworks.Zeugwerk.Version, $"{referencename}_{plc.Frameworks.Zeugwerk.Version} not found in repository cache");

                        var plcLibraryModel = new Models.PlcLibrary
                        {
                            Name = referencename,
                            Version = plc.Frameworks.Zeugwerk.Version
                        };

                        if (false == InstalledLibraries?.Any(x => x.Name == plcLibraryModel.Name && x.Version == plcLibraryModel.Version))
                        {
                            InstalledLibraries = InstalledLibraries ?? new HashSet<Models.PlcLibrary>();
                            InstalledLibraries.Add(plcLibraryModel);
                            _logger.Info($"Installing library {plcLibraryModel.Name} {plcLibraryModel.Version} ...");
                            libManager.InstallLibrary("System", path, true);
                        }
                    }
                }
            }

            // Install all System Libraries if they are not already installed
            if (plc.References != null)
            {
                List<String> references = null;
                if (plc.References.ContainsKey(tcversion))
                    references = plc.References[tcversion];
                else if (plc.References.ContainsKey("*"))
                    references = plc.References["*"];

                if (references != null)
                {
                    foreach (string reference in references)
                    {
                        string referencename = reference.Split('=').First();
                        string referenceversion = reference.Split('=').Last();

                        var path = TwinpackUtils.FindLibraryFilePathWithoutExtension(tcversion, referencename, referenceversion, targetPath: targetPath);
                        if (path == null)
                            continue;

                        var plcLibraryModel = new Models.PlcLibrary
                        {
                            Name = referencename,
                            Version = referenceversion
                        };

                        if (false == InstalledLibraries?.Any(x => x.Name == plcLibraryModel.Name && x.Version == plcLibraryModel.Version))
                        {
                            InstalledLibraries = InstalledLibraries ?? new HashSet<Models.PlcLibrary>();
                            InstalledLibraries.Add(plcLibraryModel);

                            if (File.Exists(path + "compiled-library"))
                            {
                                path += "compiled-library";
                                libManager.InstallLibrary("System", path, true);
                            }
                            else if (File.Exists(path + "library"))
                            {
                                path += "library";
                                libManager.InstallLibrary("System", path, true);
                            }
                        }
                        string vendor = null;
                        foreach (ITcPlcLibrary r in libManager.ScanLibraries())
                        {
                            if (r.Name == referencename && (r.Version == referenceversion || referenceversion == "*" || referenceversion == "newest"))
                            {
                                vendor = r.Distributor;
                                break;
                            }
                        }

                        if (referenceversion == "*")
                            _logger.Debug($"Using reference: {referencename}={referenceversion} (latest available)");
                        else if (File.Exists(path))
                            _logger.Debug($"Using reference: {referencename}={referenceversion} (from repository)");
                        else if (vendor != null)
                            _logger.Debug($"Using reference: {referencename}={referenceversion} (preinstalled)");
                        else
                            throw new DependencyNotFoundException(referencename, referenceversion, $"{referencename}={referenceversion} is not preinstalled and was not restored from any repository!");
                    }
                }
            }

            AddReferences(libManager, DefaultLibraryCachePath, plc, tcversion);
        }

        static private void AddReferences(ITcPlcLibraryManager libManager, string libraryCachePath, Models.ConfigPlcProject plc, string tcversion)
        {
            if (!string.IsNullOrEmpty(plc?.Frameworks?.Zeugwerk?.Version))
            {
                foreach (string referencename in plc.Frameworks.Zeugwerk.References)
                {
                    // always use placeholders in applications                     
                    if (plc.PlcType == Models.ConfigPlcProject.PlcProjectType.Application)
                    {
                        AddReference(libManager, referencename, referencename, plc.Frameworks.Zeugwerk.Version, Models.ConfigFactory.ZeugwerkVendorName, addAsPlaceholder: true);
                    }
                    else
                    {
                        AddReference(libManager, referencename, referencename, plc.Frameworks.Zeugwerk.Version, Models.ConfigFactory.ZeugwerkVendorName, addAsPlaceholder: true);
                    }

                    // do not add qualifed-only attributes for the testing PLC
                    if (plc.PlcType != Models.ConfigPlcProject.PlcProjectType.Application)
                    //   String.Compare(plc.Name, referencename + "Tests", ignoreCase: true) != 0)
                    {
                        // Set qualified-only for Zeugwerk Libraries
                        var qualifiedOnly = plc.Frameworks?.Zeugwerk?.QualifiedOnly ?? true;
                        var hideReference = plc.Frameworks?.Zeugwerk?.Hide ?? false;
                        var referenceItem = (libManager as ITcSmTreeItem).LookupChild(referencename);// systemManager.LookupTreeItem($"TIPC^{plc.Name}^{plc.Name} Project^References^{referencename}");
                        var referenceXml = referenceItem.ProduceXml(bRecursive: true);
                        var referenceDoc = XDocument.Parse(referenceXml);

                        var qualifiedOnlyItem = referenceDoc.Elements("TreeItem")
                            .Elements("VSProperties")
                            .Elements("VSProperty")
                            .Where(x => x.Element("Name").Value == "QualifiedlAccessOnly")
                            .Elements("Value").FirstOrDefault();
                        qualifiedOnlyItem.Value = qualifiedOnly ? "True" : "False";

                        var hideReferenceItem = referenceDoc.Elements("TreeItem")
                            .Elements("VSProperties")
                            .Elements("VSProperty")
                            .Where(x => x.Element("Name").Value == "HideReference")
                            .Elements("Value").FirstOrDefault();
                        hideReferenceItem.Value = hideReference ? "True" : "False";

                        referenceItem.ConsumeXml(referenceDoc.ToString());
                    }
                }
            }

            // create a list containing tuples in the form (library_name, library_version)
            // the list contains all framework references + twincat system libraries
            List<Tuple<string, string>> used_references = null;
            if (plc.References != null)
            {
                if (plc.References.ContainsKey(tcversion))
                    used_references = plc.References[tcversion].Select(x => new Tuple<string, string>(x.Split('=')[0], x.Split('=')[1])).ToList();
                else if (plc.References.ContainsKey("*"))
                    used_references = plc.References["*"].Select(x => new Tuple<string, string>(x.Split('=')[0], x.Split('=')[1])).ToList();
            }

            // Applications require Tc3_Module
            if (plc.PlcType == Models.ConfigPlcProject.PlcProjectType.Application)
            {
                if (used_references == null)
                    used_references = new List<Tuple<string, string>>();

                if (used_references.Find(x => x.Item1 == "Tc3_Module") == null)
                    used_references.Add(new Tuple<string, string>("Tc3_Module", "*"));
            }

            // adding references
            foreach (Tuple<string, string> reference in used_references ?? Enumerable.Empty<Tuple<string, string>>())
            {
                string vendor = "[unknown]";
                foreach (ITcPlcLibrary r in libManager.ScanLibraries())
                {
                    if (r.Name == reference.Item1 && (r.Version == reference.Item2 || reference.Item2 == "*" || reference.Item2 == "newest"))
                    {
                        vendor = r.Distributor;
                        break;
                    }
                }

                try
                {
                    // always use placeholders in applications                                               
                    if (plc.PlcType == Models.ConfigPlcProject.PlcProjectType.Application)
                    {
                        AddReference(libManager, reference.Item1, reference.Item1, reference.Item2, vendor, addAsPlaceholder: true);
                    }
                    else if (reference.Item2 == "*") // for libraries only use them, when actually requested
                    {
                        AddReference(libManager, reference.Item1, reference.Item1, reference.Item2, vendor, addAsPlaceholder: true);
                        // libManager.SetEffectiveResolution(reference.Item1, reference.Item1, reference.Item2, vendor);                        
                    }
                    else // ... but default for library references
                    {
                        AddReference(libManager, reference.Item1, reference.Item1, reference.Item2, vendor, addAsPlaceholder: false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex);

                    if (libraryCachePath != "")
                    {
                        throw new DependencyAddException(reference.Item1, reference.Item2, $"Error adding reference {reference.Item1} ({reference.Item2})");
                    }
                }

                if (String.Compare(reference.Item1, "TcUnit", ignoreCase: true) == 0)
                {
                    var tcUnitItem = (libManager as ITcSmTreeItem).LookupChild("TcUnit");
                    var libManagerXml = tcUnitItem.ProduceXml(bRecursive: true);
                    var libManagerDoc = XDocument.Parse(libManagerXml);

                    var tcUnitElement = libManagerDoc.Elements("TreeItem")
                        .Elements("PlcLibPlaceholder")
                        .Elements("PlaceholderReference")
                        .Where(x => true == x.Elements("PlaceholderName")?.Select(p => p.Value == "TcUnit")?.Any());

                    var xUnitEnablePublish = tcUnitElement.Elements("ParameterList")
                        .Elements("Parameter").Where(p => p.Element("Name").Value == "GVL_PARAM_TCUNIT.XUNITENABLEPUBLISH")?
                        .FirstOrDefault().Element("Value");

                    var xUnitFilePath = tcUnitElement.Elements("ParameterList")
                        .Elements("Parameter").Where(p => p.Element("Name").Value == "GVL_PARAM_TCUNIT.XUNITFILEPATH")?
                        .FirstOrDefault().Element("Value");

                    var xUnitLogExtendedResults = tcUnitElement.Elements("ParameterList")
                        .Elements("Parameter").Where(p => p.Element("Name").Value == "GVL_PARAM_TCUNIT.LOGEXTENDEDRESULTS")?
                        .FirstOrDefault().Element("Value");

                    var xUnitLogBufferSize = tcUnitElement.Elements("ParameterList")
                        .Elements("Parameter").Where(p => p.Element("Name").Value == "GVL_PARAM_TCUNIT.XUNITBUFFERSIZE")?
                        .FirstOrDefault().Element("Value");

                    xUnitEnablePublish.Value = "TRUE";
                    xUnitFilePath.Value = $@"'C:\TwinCAT\3.1\Boot\TcUnit_xUnit_results.xml'";
                    xUnitLogExtendedResults.Value = "FALSE";
                    xUnitLogBufferSize.Value = "1048576";

                    tcUnitItem.ConsumeXml(libManagerDoc.ToString());
                }
            }
        }

        static private async Task<RetrieveVersionResult> RestorePlcFrameworkAsync(List<string> repositories, string used_tcversion, Models.ConfigPlcProject.PlcProjectType type, List<string> references, string version, string targetPath)
        {
            RetrieveVersionResult restoreResult = new RetrieveVersionResult();
            restoreResult.ActualVersion = version;

            if (repositories?.Any() == false)
            {
                restoreResult.Success = true;
                return restoreResult;
            }

            bool restored = false;

            // try to get everything from the first given repository, but try main and release branch
            foreach (var repository in repositories)
            {
                if (references == null || references.Count() == 0)
                {
                    restoreResult.Success = true;
                    return restoreResult;
                }

                if (version == null || version.Length == 0)
                    version = "latest";

                // download framework as archive - if available, otherwise we fall back to seperate files later on
                RetrieveVersionResult downloadResult = await DownloadAndExtractFromArchiveAsync(used_tcversion, repository, references, version, targetPath);
                restoreResult.ActualVersion = downloadResult.ActualVersion;
                if (!downloadResult.Success &&
                    !await DownloadSingleFilesAsync(used_tcversion, repository, references, downloadResult.ActualVersion, targetPath) &&
                    !await DownloadSingleFilesAsync(used_tcversion, repository, references, downloadResult.ActualVersion, targetPath, forceSingleLibs: true))
                    continue;

                // download framework documentation for usage in applications or if an already available framework version is referenced
                // ${distribution_server}/bin/main/Zeugwerk_Framework-doc-${plcproj.zframework.version}.zip            
                //if (type == ConfigPlcProject.PlcProjectType.Application && frameworkDownloaded)
                //{
                //    if (!await DownloadAndExtractDocFromArchiveAsync(repository, actualVersion, targetPath))
                //        continue;
                //}

                restored = IsCached(used_tcversion, references, downloadResult.ActualVersion, targetPath);

                if (restored)
                    break;
            }


            // for features overwrite with files from the feature branches if possible
            var fixOrBranchRepositories = repositories.Where(x => x.Contains("feature/") || x.Contains("fix/"));
            foreach (var reference in references)
                foreach (var repository in fixOrBranchRepositories)
                    if (await DownloadSingleFilesAsync(used_tcversion, repository, new List<string> { reference }, version, targetPath))
                        _logger.Debug($"Using {reference} from {(reference.Contains("fix/") ? "fix" : "feature")} branch");

            restored = IsCached(used_tcversion, references, restoreResult.ActualVersion, targetPath);
            restoreResult.Success = restored;
            return restoreResult;
        }

        static private async Task RestorePlcReferenceAsync(List<string> repositories, string used_tcversion, List<string> references, string version, string targetPath)
        {
            if (references == null || references.Count() == 0)
                return;

            foreach (var repository in repositories)
            {
                _logger.Debug($"Checking {repository} for references");
                await DownloadSingleFilesAsync(used_tcversion, repository, references, null, targetPath, forceSingleLibs: true);
            }
        }

        static private List<string> PossibleBranches(string version)
        {
            var result = new List<string>();
            if (version != null && version != "*" && version != "latest" && version.Length > 0)
            {
                var split = version.Split('.');
                if (split.Length >= 3)
                    result.Add($"release/{String.Join(".", split, 0, 3)}");

                if (split.Length >= 2)
                    result.Add($"release/{String.Join(".", split, 0, 2)}");
            }

            if (UseMainBranch)
                result.Add("main");
            return result;
        }
    }
}
