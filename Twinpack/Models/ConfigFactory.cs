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

        public static Config CreateFromSolution(EnvDTE.Solution solution)
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
                        project.Plcs.Add(ConfigPlcProjectFactory.Create(projectPath));
                    }
                }

                config.Projects.Add(project);
            }

            return config;
        }

        public static Config Create(string path = ".")
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
                project.Plcs.Add(ConfigPlcProjectFactory.Create(plcpath));
            }

            config.Projects = new List<ConfigProject>();
            config.Projects.Add(project);
            return config;
        }

        public void Save(Config config)
        {
            if (config.FilePath == null)
                return;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            String json = JsonSerializer.Serialize(this, options);

            if (!Directory.Exists(Path.GetDirectoryName(config.FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(config.FilePath));

            File.WriteAllText(config.FilePath, json);
        }
    }


    public class ConfigPlcProjectFactory
    {
        static public XNamespace TcNs = "http://schemas.microsoft.com/developer/msbuild/2003";

        static public async Task<ConfigPlcProject> MapPlcConfigToPlcProj(EnvDTE.Solution solution, EnvDTE.Project plc)
        {
            ConfigPlcProject plcConfig = null;

            if (!string.IsNullOrEmpty(solution?.FullName))
            {
                var config = Models.ConfigFactory.Load(System.IO.Path.GetDirectoryName(solution.FullName));
                config = null;
                if (config == null)
                    config = Models.ConfigFactory.CreateFromSolution(solution);

                string projectName = null;
                foreach (EnvDTE.Project prj in solution.Projects)
                {
                    try
                    {
                        ITcSmTreeItem plcs = (prj.Object as ITcSysManager).LookupTreeItem("TIPC");
                        foreach (ITcSmTreeItem9 p in plcs)
                            if (Object.ReferenceEquals((plc.Object as dynamic).Parent, p))
                            {
                                projectName = plc.Name;
                                break;
                            }
                    }
                    catch (Exception) { }
                }

                plcConfig = config.Projects.Where(x => x.Name == projectName).SelectMany(x => x.Plcs).Where(x => x.Name == plc.Name).FirstOrDefault();
            }

            return plcConfig;
        }

        public static ConfigPlcProject Create(string plcProjFilepath)
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
            plc.IconUrl = "";
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

            SetLibraryReferences(plc, xdoc);

            return plc;
        }

        public static void UpdateLibraryReferences(ConfigPlcProject plc)
        {
            XDocument xdoc = XDocument.Load(GuessFilePath(plc));
            SetLibraryReferences(plc, xdoc);
        }

        private static void SetLibraryReferences(ConfigPlcProject plc, XDocument xdoc)
        {
            // collect references
            var references = new List<Tuple<string, string, string>>();
            var re = new Regex(@"(.*?),(.*?) \((.*?)\)");
            var lst = xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "PlaceholderResolution").Elements(TcNs + "Resolution");
            foreach (XElement g in lst)
            {
                var match = re.Match(g.Value);
                if (match.Success)
                    references.Add(new Tuple<string, string, string>(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim(), match.Groups[3].Value.Trim()));
            }

            //ZExperimental,0.8.1.63,Zeugwerk GmbH
            re = new Regex(@"(.*?),(.*?),(.*?)");
            IEnumerable<XAttribute> lst1 = null;
            lst1 = xdoc.Elements(TcNs + "Project").Elements(TcNs + "ItemGroup").Elements(TcNs + "LibraryReference").Attributes(TcNs + "Include");
            foreach (XAttribute g in lst1)
            {
                var match = re.Match(g.Value);
                if (match.Success)
                    references.Add(new Tuple<string, string, string>(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim(), match.Groups[3].Value.Trim()));
            }

            var sysref = new List<Tuple<string, string>>();

            plc.Frameworks = plc.Frameworks ?? new ConfigFrameworks();
            plc.Frameworks.Zeugwerk = plc.Frameworks.Zeugwerk ?? new ConfigFramework();
            plc.Frameworks.Zeugwerk.References = new List<string>();
            plc.Frameworks.Zeugwerk.Hide = false;
            foreach (var r in references)
            {
                if (r.Item3.Contains("Zeugwerk GmbH"))
                {
                    plc.Frameworks.Zeugwerk.References.Add(r.Item1);
                    plc.Frameworks.Zeugwerk.Version = r.Item2;
                }
                else
                {
                    sysref.Add(new Tuple<string, string>(r.Item1, r.Item2));
                }
            }

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
                plc.References["*"].Add($"{r.Item1}={r.Item2}");
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