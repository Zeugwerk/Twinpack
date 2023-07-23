using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        static public readonly string ZeugwerkVendorName = "Zeugwerk GmbH";
        static public readonly List<String> DefaultLocations = new List<String> { $@".\", $@".\.Zeugwerk\" };

        public static Config Load(string path = ".")
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
                    config.WorkingDirectory = path;
                    config.FilePath = $@"{path}\{p}config.json";

                    foreach (var project in config.Projects)
                    {
                        foreach (var plc in project.Plcs)
                        {
                            plc.RootPath = config.WorkingDirectory;
                            plc.ProjectName = project.Name;
                        }
                    }

                    return config;
                }
            }

            return null;
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

        public static async Task<Config> CreateFromSolutionAsync(EnvDTE.Solution solution)
        {
            Config config = new Config();

            config.Fileversion = 1;
            config.Solution = solution.FileName;
            config.FilePath = Path.GetDirectoryName(solution.FullName);
            config.WorkingDirectory = Path.GetDirectoryName(solution.FullName);
            config.Projects = new List<ConfigProject>();

            foreach (EnvDTE.Project prj in solution.Projects)
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
                        var plcConfig = await ConfigPlcProjectFactory.CreateAsync(projectPath);
                        plcConfig.ProjectName = project.Name;
                        plcConfig.RootPath = config.WorkingDirectory;
                        project.Plcs.Add(plcConfig);
                    }
                }

                config.Projects.Add(project);
            }

            return config;
        }

        public static async Task<Config> CreateAsync(string path = ".")
        {
            Config config = new Config();
            var solutions = Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories);

            if (solutions.Count() > 1)
                _logger.Warn("There is more than 1 solution present in the current directory, only the first one is considered!");
            else if (solutions.Any() == false)
                return null;

            config.Fileversion = 1;
            config.Solution = Path.GetFileName(solutions.First());
            config.FilePath = Path.GetDirectoryName(solutions.First()) + @"\.Zeugwerk\config.json";
            config.WorkingDirectory = path;

            var project = new ConfigProject();
            project.Name = config.Solution?.Split('.').First();
            project.Plcs = new List<ConfigPlcProject>();

            foreach (var plcpath in Directory.GetFiles(path, "*.plcproj", SearchOption.AllDirectories))
            {
                project.Plcs.Add(await ConfigPlcProjectFactory.CreateAsync(plcpath));
            }

            config.Projects = new List<ConfigProject>();
            config.Projects.Add(project);
            return config;
        }

        public static void Save(Config config)
        {
            if (config.FilePath == null)
                return;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(config, options);

            if (!Directory.Exists(Path.GetDirectoryName(config.FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(config.FilePath));

            File.WriteAllText(Path.Combine(config.WorkingDirectory, ".Zeugwerk", "config.json"), json);
        }
    }


    public class ConfigPlcProjectFactory
    {
        static public XNamespace TcNs = "http://schemas.microsoft.com/developer/msbuild/2003";

        static public ConfigPlcProject MapPlcConfigToPlcProj(Config config, EnvDTE.Project prj)
        {
            var project = new ConfigProject { Name = prj.Name };
            var xml = (prj as ITcSmTreeItem9).ProduceXml();

            if (xml != null)
            {
                string projectPath = XElement.Parse(xml).Element("PlcProjectDef").Element("ProjectPath").Value;
                var plcName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
                return config.Projects.FirstOrDefault(x => x.Name == prj.Name)?.Plcs?.FirstOrDefault(x => x.Name == plcName);
            }

            return null;
        }

        static public async Task<ConfigPlcProject> CreateAsync(EnvDTE.Solution solution, EnvDTE.Project prj, TwinpackServer twinpackServer)
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
                var plcConfig = await CreateAsync(projectPath, twinpackServer);
                plcConfig.ProjectName = prj.Name;
                plcConfig.RootPath = System.IO.Path.GetDirectoryName(solution.FullName);
                return plcConfig;
            }

            return null;
        }

        public static async Task<ConfigPlcProject> CreateAsync(string plcProjFilepath, TwinpackServer twinpackServer=null)
        {
            var plc = new ConfigPlcProject();
            plc.FilePath = plcProjFilepath;
            XDocument xdoc = XDocument.Load(GuessFilePath(plc));

            plc.Name = System.IO.Path.GetFileNameWithoutExtension(plcProjFilepath);
            plc.Packages = new List<ConfigPlcPackage>();
            //plc.Name = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "Name")?.FirstOrDefault()?.Value;
            plc.Version = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Version")?.FirstOrDefault()?.Value;
            plc.Authors = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Author")?.FirstOrDefault()?.Value;
            plc.Description = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Description")?.FirstOrDefault()?.Value;
            plc.DistributorName = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Company")?.FirstOrDefault()?.Value;
            plc.IconFile = "";
            plc.DisplayName = plc.Name;
            plc.ProjectUrl = "";

            // Fallback
            plc.Version = plc.Version ?? xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "ProjectVersion").FirstOrDefault()?.Value;
            plc.Version = plc.Version ?? "1.0.0.0";

            // heuristics to find the plc type, if there is a task in the plc it is most likely an application, if not it is a library. Library that are coming from
            // Zeugwerk are most likely Framework Libraries
            var company = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "Company").FirstOrDefault()?.Value;
            var tasks = xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "Compile")
                .Where(x => x.Attribute("Include") != null && x.Attribute("Include").Value.EndsWith("TcTTO"));
            if (tasks.Count() > 0)
            {
                plc.Type = ConfigPlcProject.PlcProjectType.Application.ToString();
            }
            else if (company == "Zeugwerk GmbH")
            {
                plc.Type = ConfigPlcProject.PlcProjectType.FrameworkLibrary.ToString();
            }
            else
            {
                plc.Type = ConfigPlcProject.PlcProjectType.Library.ToString();
            }

            await SetLibraryReferencesAsync (plc, xdoc, twinpackServer);

            return plc;
        }

        public static async Task UpdateLibraryReferencesAsync(ConfigPlcProject plc, TwinpackServer twinpackServer)
        {
            XDocument xdoc = XDocument.Load(GuessFilePath(plc));
            await SetLibraryReferencesAsync(plc, xdoc, twinpackServer);
        }

        private static async Task SetLibraryReferencesAsync(ConfigPlcProject plc, XDocument xdoc, TwinpackServer twinpackServer)
        {
            // collect references
            var references = new List<PlcLibrary>();
            var re = new Regex(@"(.*?),(.*?) \((.*?)\)");
            var lst = xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "PlaceholderResolution").Elements(TcNs + "Resolution");
            foreach (XElement g in lst)
            {
                var match = re.Match(g.Value);
                if (match.Success)
                    references.Add(new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() });
            }

            //ZExperimental,0.8.1.63,Zeugwerk GmbH
            re = new Regex(@"(.*?),(.*?),(.*?)");
            IEnumerable<XAttribute> lst1 = null;
            lst1 = xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "LibraryReference").Attributes(TcNs + "Include");
            foreach (XAttribute g in lst1)
            {
                var match = re.Match(g.Value);
                if (match.Success)
                    references.Add(new PlcLibrary { Name = match.Groups[1].Value.Trim(), Version = match.Groups[2].Value.Trim(), DistributorName = match.Groups[3].Value.Trim() });
            }

            var sysref = new List<PlcLibrary>();
            var packages = new List<ConfigPlcPackage>();

            plc.Frameworks = plc.Frameworks ?? new ConfigFrameworks();
            plc.Frameworks.Zeugwerk = plc.Frameworks.Zeugwerk ?? new ConfigFramework();
            plc.Frameworks.Zeugwerk.References = new List<string>();
            plc.Frameworks.Zeugwerk.Hide = false;
            
            foreach (var r in references)
            {
                // check if we find this on Twinpack
                bool isTwinpackPackage = false;

                if(twinpackServer != null)
                {
                    try
                    {
                        var packageVersion = await twinpackServer.ResolvePackageVersionAsync(r);
                        if(isTwinpackPackage = packageVersion.Repository != null && packageVersion.Name != null && packageVersion.DistributorName != null)
                        {
                            packages.Add(new ConfigPlcPackage
                            {
                                DistributorName = packageVersion.DistributorName,
                                Repository = packageVersion.Repository,
                                Branch = packageVersion.Branch,
                                Configuration = packageVersion.Configuration,
                                Name = packageVersion.Name,
                                Target = packageVersion.Target,
                                Version = r.Version
                            });
                        }
                    }
                    catch (Exception) { }
                }


                if (r.DistributorName.Contains("Zeugwerk GmbH")) // needed for backwards compability for now (devkit release/1.2)
                {
                    plc.Frameworks.Zeugwerk.References.Add(r.Name);
                    plc.Frameworks.Zeugwerk.Version = r.Version;
                }
                else if(!isTwinpackPackage)
                {
                    sysref.Add(r);
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
            foreach (var r in sysref.Distinct())
            {
                plc.References["*"].Add($"{r.Name}={r.Version}");
            }
        }


        private static String GuessFilePath(ConfigPlcProject plc)
        {
            // todo: parse sln, ts(p)proj and xti to get the path of the PLC instead of guessing
            if (plc.FilePath != null)
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
            if (plc.Namespace != null)
                return plc.Namespace;

            XDocument xdoc = XDocument.Load(GuessFilePath(plc));
            plc.Namespace = xdoc.Elements(TcNs + "Project").Elements(TcNs + "PropertyGroup").Elements(TcNs + "DefaultNamespace").FirstOrDefault()?.Value;

            if(plc.Namespace == null)
                plc.Namespace = plc.Name;

            return plc.Namespace;
        }

        public static XDocument XDoc(ConfigPlcProject plc)
        {
            return XDocument.Load(GuessFilePath(plc));
        }
    }
}
