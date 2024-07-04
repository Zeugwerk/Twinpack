using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NLog;
using TCatSysManagerLib;

namespace Twinpack.Models
{
    public class ConfigFactory
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public const string DefaultRepository = "https://framework.zeugwerk.dev/Distribution";
        
        public static readonly string ZeugwerkVendorName = "Zeugwerk GmbH";
        public static readonly List<string> DefaultLocations = new List<String> { $@"", $@".Zeugwerk\" };
        public static readonly XNamespace TsProjectNs = "http://www.w3.org/2001/XMLSchema-instance";

        public static Config Load(string path = ".", bool validate=false)
        {
            Config config = null;
            var usedPrefix = "";
            path = Path.IsPathRooted(path) ? path : $@".\{path}";
            foreach (var p in DefaultLocations)
            {
                if (File.Exists($@"{path}\{p}config.json"))
                {
                    if (usedPrefix.Length != 0)
                        throw new Exception("found multiple configuration files");

                    usedPrefix = p;
                    config = new Config();
                    config = JsonSerializer.Deserialize<Config>(File.ReadAllText($@"{path}\{p}config.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    config.WorkingDirectory = Path.GetDirectoryName($@"{path}\{config.Solution}");
                    config.FilePath = $@"{path}\{p}config.json";

                    var solution = new Solution();
                    try
                    {
                        solution.Load($@"{path}\" + config.Solution);
                    }
                    catch(FileNotFoundException)
                    {
                        if(validate)
                            throw;
                    }

                    foreach (var project in config.Projects)
                    {
                        foreach (var plc in project.Plcs)
                        {
                            var plcpath = solution.Projects.Where(x => x.Name == project.Name).FirstOrDefault()?.Plcs.Where(x => x.Name == plc.Name)?.FirstOrDefault()?.FilePath;

                            plc.RootPath = config.WorkingDirectory;
                            plc.ProjectName = project.Name;
                            plc.FilePath = plcpath ?? ConfigPlcProjectFactory.GuessFilePath(plc);
                        }
                    }

                    return config;
                }
            }

            return config;
        }

        public static Config Create(string solutionName, List<ConfigProject> projects = null, string filepath = null)
        {
            var config = new Config();

            config.Solution = solutionName;
            config.Fileversion = 1;
            config.FilePath = filepath ?? $@"{Environment.CurrentDirectory}\.Zeugwerk\config.json";
            config.WorkingDirectory = filepath == null ? Environment.CurrentDirectory : Path.GetDirectoryName(filepath.Replace(@".Zeugwerk\config.json", ""));
            config.Projects = projects ?? new List<ConfigProject>();

            return config;
        }

        public static Task<Config> CreateFromSolutionAsync(EnvDTE.Solution solution, Protocol.IPackageServer packageServer, IEnumerable<ConfigPlcProject.PlcProjectType> plcTypeFilter = null, CancellationToken cancellationToken = default)
        {
            return CreateFromSolutionAsync(solution, packageServer, plcTypeFilter, cancellationToken);
        }

        public static async Task<Config> CreateFromSolutionAsync(EnvDTE.Solution solution, IEnumerable<Protocol.IPackageServer> packageServers, IEnumerable<ConfigPlcProject.PlcProjectType> plcTypeFilter = null, CancellationToken cancellationToken = default)
        {
            Config config = new Config();

            config.Fileversion = 1;
            config.Solution = Path.GetFileName(solution.FileName);
            config.FilePath = Path.GetDirectoryName(solution.FullName) + @"\.Zeugwerk\config.json";
            config.WorkingDirectory = Path.GetDirectoryName(solution.FullName);
            config.Projects = new List<ConfigProject>();

            foreach (EnvDTE.Project prj in solution.Projects)
            {
                ITcSysManager2 systemManager = prj?.Object as ITcSysManager2;
                if (systemManager == null)
                    continue;

                var project = new ConfigProject();
                project.Name = prj.Name;

                ITcSmTreeItem plcs = systemManager.LookupTreeItem("TIPC");
                foreach (ITcSmTreeItem9 plc in plcs)
                {
                    if (plc is ITcProjectRoot)
                    {
                        string xml = plc.ProduceXml();
                        string projectPath = XElement.Parse(xml).Element("PlcProjectDef").Element("ProjectPath").Value;
                        var plcConfig = await ConfigPlcProjectFactory.CreateAsync(projectPath, packageServers, cancellationToken);
                        plcConfig.ProjectName = project.Name;
                        plcConfig.RootPath = config.WorkingDirectory;
                        plcConfig.FilePath = ConfigPlcProjectFactory.GuessFilePath(plcConfig);

                        if (plcTypeFilter == null || plcTypeFilter.Contains(plcConfig.PlcType))
                            project.Plcs.Add(plcConfig);
                    }
                }

                config.Projects.Add(project);
            }

            return config;
        }

        public static async Task<Config> CreateFromSolutionFileAsync(string path = ".", bool continueWithoutSolution = false, Protocol.IPackageServer packageServer = null, IEnumerable<ConfigPlcProject.PlcProjectType> plcTypeFilter=null, CancellationToken cancellationToken = default)
        {

            Config config = new Config();
            var solutions = Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories);

            if (solutions.Count() > 1)
                _logger.Warn("There is more than 1 solution present in the current directory, only the first one is considered!");
            else if (solutions.Any() == false && continueWithoutSolution == false)
                return null;

            config.Fileversion = 1;
            config.WorkingDirectory = path;

            if(solutions.Any())
            {
                config.Solution = Path.GetFileName(solutions.First());
                config.FilePath = Path.GetDirectoryName(solutions.First()) + @"\.Zeugwerk\config.json";         
            }
            else
            {
                config.FilePath = $@"{Environment.CurrentDirectory}\.Zeugwerk\config.json";
            }

            var solution = Solution.LoadFromFile(solutions.First());
            
            foreach(var project in solution.Projects)
            {
                var projectConfig = new ConfigProject();
                projectConfig.Name = project.Name;
                projectConfig.Plcs = new List<ConfigPlcProject>();

                foreach (var plc in project.Plcs)
                {
                    var plcConfig = await ConfigPlcProjectFactory.CreateAsync(plc.FilePath, packageServer, cancellationToken);

                    if(plcTypeFilter == null || plcTypeFilter.Contains(plcConfig.PlcType))
                        projectConfig.Plcs.Add(plcConfig);
                }
                
                if(projectConfig.Plcs.Any())
                    config.Projects.Add(projectConfig);
            }

            return config;
        }

        public static string Save(Config config)
        {
            if (config.FilePath == null)
                return null;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(config, options);

            if (!Directory.Exists(Path.GetDirectoryName(config.FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(config.FilePath));

            File.WriteAllText(config.FilePath, json);

            return config.FilePath;
        }

        public static void UpdatePlcProject(Config config, ConfigPlcProject plcConfig)
        {
            if (config == null || plcConfig == null)
            {
                _logger.Warn($"The solution doesn't have a package configuration");
                return;
            }

            _logger.Info($"Updating package configuration {Path.GetFullPath(config.FilePath)}");

            var projectIndex = config.Projects.FindIndex(x => x.Name == plcConfig.ProjectName);
            if (projectIndex < 0)
            {
                config.Projects.Add(new Models.ConfigProject { Name = plcConfig.ProjectName, Plcs = new List<ConfigPlcProject> { plcConfig } });
            }
            else
            {
                var plcIndex = config.Projects[projectIndex].Plcs.FindIndex(x => x.Name == plcConfig.Name);
                
                if (plcIndex < 0)
                    config.Projects[projectIndex].Plcs.Add(plcConfig);
                 else
                     config.Projects[projectIndex].Plcs[plcIndex] = plcConfig;
            }
        }
    }


    public class ConfigPlcProjectFactory
    {
        public static XNamespace TcNs = "http://schemas.microsoft.com/developer/msbuild/2003";

        public static ConfigPlcProject MapPlcConfigToPlcProj(Config config, EnvDTE.Project prj)
        {
            string xml = null;
            ITcSysManager2 systemManager = (prj.Object as dynamic).SystemManager as ITcSysManager2;
            ITcSmTreeItem plcs = systemManager.LookupTreeItem("TIPC");
            foreach (ITcSmTreeItem9 plc in plcs)
            {
                if (plc is ITcProjectRoot && plc.Name == prj.Name)
                {
                    xml = plc.ProduceXml();
                    break;
                }
            }

            if (xml != null)
            {
                string projectPath = XElement.Parse(xml).Element("PlcProjectDef").Element("ProjectPath").Value;
                var plcName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
                return config.Projects.SelectMany(x => x.Plcs).FirstOrDefault(x => x.Name == plcName);
                //return config.Projects.FirstOrDefault(x => x.Name == prj.Name)?.Plcs?.FirstOrDefault(x => x.Name == plcName);
            }

            return null;
        }

        public static Task<ConfigPlcProject> CreateAsync(EnvDTE.Solution solution, EnvDTE.Project prj, Protocol.IPackageServer packageServer, CancellationToken cancellationToken = default)
        {
            return CreateAsync(solution, prj, new List<Protocol.IPackageServer> { packageServer }, cancellationToken);
        }

        public static async Task<ConfigPlcProject> CreateAsync(EnvDTE.Solution solution, EnvDTE.Project prj, IEnumerable<Protocol.IPackageServer> packageServers, CancellationToken cancellationToken = default)
        {
            ITcSysManager2 systemManager = (prj.Object as dynamic).SystemManager as ITcSysManager2;
            var project = new ConfigProject();
            project.Name = prj.Name;

            string xml = null;
            ITcSmTreeItem plcs = systemManager.LookupTreeItem("TIPC");
            foreach (ITcSmTreeItem9 plc in plcs)
            {
                if (plc is ITcProjectRoot && plc.Name == prj.Name)
                {
                    xml = plc.ProduceXml();
                    break;
                }
            }

            if(xml != null)
            {
                string projectPath = XElement.Parse(xml).Element("PlcProjectDef").Element("ProjectPath").Value;
                var plcConfig = await CreateAsync(projectPath, packageServers, cancellationToken);
                plcConfig.ProjectName = prj.Name;
                plcConfig.RootPath = System.IO.Path.GetDirectoryName(solution.FullName);
                plcConfig.FilePath = ConfigPlcProjectFactory.GuessFilePath(plcConfig);                  
                return plcConfig;
            }

            return null;
        }

        public static Task<ConfigPlcProject> CreateAsync(string plcProjFilepath, Protocol.IPackageServer packageServer, CancellationToken cancellationToken = default)
        {
            return CreateAsync(plcProjFilepath, new List<Protocol.IPackageServer> { packageServer }, cancellationToken);
        }

        public static async Task<ConfigPlcProject> CreateAsync(string plcProjFilepath, IEnumerable<Protocol.IPackageServer> packageServers, CancellationToken cancellationToken = default)
        {
            var plc = new ConfigPlcProject();
            plc.FilePath = plcProjFilepath;
            XDocument xdoc = XDocument.Load(GuessFilePath(plc));

            plc.Name = System.IO.Path.GetFileNameWithoutExtension(plcProjFilepath);
            plc.Title = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Title")?.FirstOrDefault()?.Value ?? plc.Name;
            plc.Packages = new List<ConfigPlcPackage>();
            //plc.Name = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "Name")?.FirstOrDefault()?.Value;
            plc.Version = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Version")?.FirstOrDefault()?.Value;
            plc.Authors = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Author")?.FirstOrDefault()?.Value;
            plc.Description = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Description")?.FirstOrDefault()?.Value;
            plc.DistributorName = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Company")?.FirstOrDefault()?.Value;
            plc.IconFile = "";
            plc.DisplayName = plc.Title;
            plc.ProjectUrl = "";

            // Fallback
            plc.Version = plc.Version ?? xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "ProjectVersion").FirstOrDefault()?.Value;
            plc.Version = plc.Version ?? "1.0.0.0";

            await SyncPackagesAndReferencesAsync(plc, xdoc, packageServers, cancellationToken);
            plc.Type = GuessPlcType(plc).ToString();

            return plc;
        }

        public static ConfigPlcProject.PlcProjectType GuessPlcType(ConfigPlcProject plc)
        {
            // heuristics to find the plc type, if there is a task in the plc it is most likely an application, if not it is a library. Library that are coming from
            // Zeugwerk are most likely Framework Libraries
            XDocument xdoc = XDocument.Load(GuessFilePath(plc));
            var company = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Company").FirstOrDefault()?.Value;
            var tasks = xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "Compile")
                .Where(x => x.Attribute("Include") != null && x.Attribute("Include").Value.EndsWith("TcTTO"));

            if (tasks.Count() > 0)
            {
                return (plc.Packages.Any(x => x.Name == "TcUnit") || plc.References["*"].Any(x => x.StartsWith("TcUnit=")))
                    ? ConfigPlcProject.PlcProjectType.UnitTestApplication
                    : ConfigPlcProject.PlcProjectType.Application;
            }
            else if (company == "Zeugwerk GmbH")
            {
                return ConfigPlcProject.PlcProjectType.FrameworkLibrary;
            }
            else
            {
                return ConfigPlcProject.PlcProjectType.Library;
            }
        }

        private static async Task SyncPackagesAndReferencesAsync(ConfigPlcProject plc, XDocument xdoc, IEnumerable<Protocol.IPackageServer> packageServers, CancellationToken cancellationToken = default)
        {
            AddPlcLibraryOptions ParseOptions(XElement element)
            {
                var ret = new AddPlcLibraryOptions
                {
                    Optional = bool.TryParse(element.Element(TcNs + "Optional")?.Value, out var optional) && optional,
                    HideWhenReferencedAsDependency = bool.TryParse(element.Element(TcNs + "HideWhenReferencedAsDependency")?.Value, out var hideWhenReferenced) && hideWhenReferenced,
                    PublishSymbolsInContainer = bool.TryParse(element.Element(TcNs + "PublishSymbolsInContainer")?.Value, out var publishSymbols) && publishSymbols,
                    QualifiedOnly = bool.TryParse(element.Element(TcNs + "QualifiedOnly")?.Value, out var qualifiedOnly) && qualifiedOnly
                };

                if (ret.Optional == false && ret.PublishSymbolsInContainer == false && ret.HideWhenReferencedAsDependency == false && ret.QualifiedOnly == false)
                    return null;

                return ret;
            }

            // collect references
            var references = new List<PlcLibrary>();
            var re = new Regex(@"(.*?),(.*?) \((.*?)\)");
            foreach (XElement g in xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "PlaceholderResolution").Elements(TcNs + "Resolution"))
            {
                var match = re.Match(g.Value);
                if (match.Success)
                {
                    var version = match.Groups[2].Value.Trim();
                    references.Add(new PlcLibrary {
                        Name = match.Groups[1].Value.Trim(), 
                        Version = version == "*" ? null : version, 
                        DistributorName = match.Groups[3].Value.Trim(),
                        Options = ParseOptions(g.Parent)
                    });
                }
            }

            foreach (XElement g in xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "PlaceholderReference").Elements(TcNs + "DefaultResolution"))
            {
                var match = re.Match(g.Value);
                if (match.Success && references.Any(x => x.Name == match.Groups[1].Value.Trim()) == false)
                {
                    var version = match.Groups[2].Value.Trim();
                    references.Add(new PlcLibrary
                    { 
                        Name = match.Groups[1].Value.Trim(), 
                        Version = version == "*" ? null : version, 
                        DistributorName = match.Groups[3].Value.Trim(),
                        Options = ParseOptions(g.Parent)
                    });
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
                    var version = match.Groups[2].Value.Trim();
                    references.Add(new PlcLibrary { 
                        Name = match.Groups[1].Value.Trim(), 
                        Version = version == "*" ? null : version, 
                        DistributorName = match.Groups[3].Value.Trim(),
                        Options = ParseOptions(g)
                    });
                }
            }

            var systemReferences = new List<PlcLibrary>();
            var packages = new List<ConfigPlcPackage>();

            plc.Frameworks = plc.Frameworks ?? new ConfigFrameworks();
            plc.Frameworks.Zeugwerk = plc.Frameworks.Zeugwerk ?? new ConfigFramework();
            plc.Frameworks.Zeugwerk.References = new List<string>();
            plc.Frameworks.Zeugwerk.Hide = false;
            
            foreach (var r in references)
            {
                // check if we find this on Twinpack
                bool isPackage = false;

                foreach(var packageServer in packageServers)
                {
                    try
                    {
                        var packageVersion = await packageServer.ResolvePackageVersionAsync(r, cancellationToken: cancellationToken);
                        if(isPackage = packageVersion.Name != null && packageVersion.DistributorName != null)
                        {
                            packages.Add(new ConfigPlcPackage
                            {
                                DistributorName = packageVersion.DistributorName,
                                Repository = packageVersion.Repository,
                                Branch = packageVersion.Branch,
                                Configuration = packageVersion.Configuration,
                                Name = packageVersion.Name,
                                Target = packageVersion.Target,
                                Version = r.Version,
                                Options = r.Options
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception) { }

                    if (isPackage)
                        break;
                }


                if (r.DistributorName.Contains("Zeugwerk GmbH")) // needed for backwards compability for now (devkit release/1.2)
                {
                    plc.Frameworks.Zeugwerk.References.Add(r.Name);
                    plc.Frameworks.Zeugwerk.Version = r.Version;
                }
                else if(!isPackage)
                {
                    systemReferences.Add(r);
                }
            }

            plc.Packages = packages;
            plc.Frameworks.Zeugwerk.References = plc.Frameworks.Zeugwerk.References.Distinct().ToList();

            if (plc.Frameworks.Zeugwerk.Repositories.Count == 0)
                plc.Frameworks.Zeugwerk.Repositories = new List<string> { ConfigFactory.DefaultRepository };


            plc.Bindings = plc.Bindings ?? new Dictionary<string, List<string>>();
            plc.Repositories = plc.Repositories ?? new List<string>();
            plc.Patches = plc.Patches ?? new ConfigPatches();
            plc.References = new Dictionary<string, List<string>>();
            plc.References["*"] = new List<string>();
            foreach (var r in systemReferences.Distinct())
            {
                plc.References["*"].Add($"{r.Name}={r.Version}");
            }
        }


        public static string GuessFilePath(ConfigPlcProject plc)
        {
            // todo: parse sln, ts(p)proj and xti to get the path of the PLC instead of guessing
            if (!string.IsNullOrEmpty(plc.FilePath))
                return plc.FilePath;

            String plcproj_path = $"{plc.RootPath}\\{plc.ProjectName}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            string slnFolder = new DirectoryInfo(plc.RootPath).Name;
            plcproj_path = $"{plc.RootPath}\\{slnFolder}\\{plc.ProjectName}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{plc.RootPath}\\{plc.ProjectName}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{plc.RootPath}\\{plc.ProjectName}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{plc.RootPath}\\{plc.ProjectName}\\{plc.Name}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{plc.RootPath}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            return null;
        }

        public static String Path(ConfigPlcProject plc)
        {
            FileInfo fi = new FileInfo(GuessFilePath(plc));
            return fi.DirectoryName;
        }

        public static string Namespace(ConfigPlcProject plc)
        {
            XDocument xdoc = XDoc(plc);
            return xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "DefaultNamespace").FirstOrDefault()?.Value;
        }

        public static XDocument XDoc(ConfigPlcProject plc)
        {
            return XDocument.Load(GuessFilePath(plc));
        }
    }
}
