using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NLog;

class ConfigFactory
{
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public const string DefaultRepository = "https://framework.zeugwerk.dev/Distribution";
        static public XNamespace TcNs = "http://schemas.microsoft.com/developer/msbuild/2003";
        static public readonly List<String> DefaultLocations = new List<String> { $@".\", $@".\.Zeugwerk\" };
        
        public static Config Load(string path = ".")
        {
            Config config = null;
            path = Path.IsPathRooted(path) ? path : $@".\{path}";
            String usedPrefix = "";
            foreach (var p in DefaultLocations)
            {
                if (File.Exists($@"{path}\{p}config.json"))
                {
                    if (usedPrefix.Length != 0)
                        throw new Exception("found multiple configuration files");

                    usedPrefix = p;
                    config = JsonSerializer.Deserialize<Config>(File.ReadAllText($@"{path}\{p}config.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    config.WorkingDirectory = path;
                    config.Filepath = $@"{path}\{p}config.json";

                    if (config.Projects != null)
                    {
                        foreach (var project in Projects)
                        {
                            foreach (var plc in project.Plcs)
                            {
                                plc.RootPath = config.WorkingDirectory;
                                plc.ProjectName = project.Name;
                            }
                        }
                    }
                }
            }

            // check if we found a valid config
            if (config == null)
                throw new FileNotFoundException($"config.json not found in any commonly used path ({String.Join(",", DefaultLocations)})");

            return config;
        }

        public static Config Create(string solutionName, List<ConfigProject> projects = null, string filepath = null)
        {
            var config = new Config();

            Config.Solution = solutionName;
            Config.Fileversion = 1;
            Config.FilePath = FilePath ?? $@"{Environment.CurrentDirectory}\.Zeugwerk\config.json";
            Config.WorkingDirectory = filepath == null ? Environment.CurrentDirectory : Path.GetDirectoryName(filepath.Replace(@".Zeugwerk\config.json", ""));
            Config.Projects = projects ?? new List<ConfigProject>();
        }

        public static Config Create(string path = ".")
        {
            Config config = new Config();
            config.WorkingDirectory = path;
            var solutions = Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories);

            if (solutions.Count() > 1)
                _logger.Warn("There is more than 1 solution present in the current directory, only the first one is considered!");

            if(solutions.Length > 0)
            {
                config.Solution = Path.GetFileName(solutions.First());
                config.FilePath = Path.GetDirectoryName(solutions.First()) + @"\.Zeugwerk\config.json";
            }

            config.Fileversion = 1;

            var project = new ConfigProject();
            project.Name = config.Solution?.Split('.').First();
            project.Plcs = new List<ConfigPlcProject>();

            foreach (var plcpath in Directory.GetFiles(path, "*.plcproj", SearchOption.AllDirectories))
            {
                project.Plcs.Add(new ConfigPlcProject(plcpath));
            }

            config.Projects = new List<ConfigProject>();
            config.Projects.Add(project);
            return config;
        }

        public void Save(Config config)
        {
            if (config.Filepath == null)
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


class ConfigPlcProjectFactory
{
        public static ConfigPlcProject Create(string plcProjFilepath)
        {
            var plc = new ConfigPlcProject();
            plc.FilePath = plcProjFilepath;
            XDocument xdoc = XDocument.Load(GuessFilePath(plc));

            plc.Name = Path.GetFileNameWithoutExtension(plcProjFilepath);
            plc.Packages = new List<ConfigPlcPackage>();
            //plc.Name = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "Name")?.FirstOrDefault()?.Value;
            plc.Version = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "Version")?.FirstOrDefault()?.Value;
            plc.Authors = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "Author")?.FirstOrDefault()?.Value;
            plc.Description = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "Description")?.FirstOrDefault()?.Value;      
            plc.IconUrl = "";
            plc.DisplayName = Name;
            plc.ProjectUrl = "";            

            // Fallback
            plc.Version = Version ?? xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "ProjectVersion").FirstOrDefault()?.Value;
            plc.Version = Version ?? "1.0.0.0";

            // heuristics to find the plc type, if there is a task in the plc it is most likely an application, if not it is a library. Library that are coming from
            // Zeugwerk are most likely Framework Libraries
            var company = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "Company").FirstOrDefault()?.Value;
            var tasks = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "ItemGroup").Elements(Config.TcNs + "Compile")
                .Where(x => x.Attribute("Include") != null && x.Attribute("Include").Value.EndsWith("TcTTO"));
            if (tasks.Count() > 0)
            {
                plc.Type = PlcProjectType.Application.ToString();
            }
            else if (company == "Zeugwerk GmbH")
            {
                plc.Type = PlcProjectType.FrameworkLibrary.ToString();
            }
            else
            {
                plc.Type = PlcProjectType.Library.ToString();
            }

            SetLibraryReferences(plc, xdoc);
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
            var lst = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "ItemGroup").Elements(Config.TcNs + "PlaceholderResolution").Elements(Config.TcNs + "Resolution");
            foreach (XElement g in lst)
            {
                var match = re.Match(g.Value);
                if (match.Success)
                    references.Add(new Tuple<string, string, string>(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim(), match.Groups[3].Value.Trim()));
            }

            //ZExperimental,0.8.1.63,Zeugwerk GmbH
            re = new Regex(@"(.*?),(.*?),(.*?)");
            IEnumerable<XAttribute> lst1 = null;
            lst1 = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "ItemGroup").Elements(Config.TcNs + "LibraryReference").Attributes(Config.TcNs + "Include");
            foreach (XAttribute g in lst1)
            {
                var match = re.Match(g.Value);
                if (match.Success)
                    references.Add(new Tuple<string, string, string>(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim(), match.Groups[3].Value.Trim()));
            }

            var sysref = new List<Tuple<string, string>>();

            plc.Frameworks = Frameworks ?? new ConfigFrameworks();
            plc.Frameworks.Zeugwerk = Frameworks.Zeugwerk ?? new ConfigFramework();
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

            plc.Frameworks.Zeugwerk.References = Frameworks.Zeugwerk.References.Distinct().ToList();

            if (plc.Frameworks.Zeugwerk.Repositories.Count == 0)
              plc.Frameworks.Zeugwerk.Repositories = new List<string> { Config.DefaultRepository };

            plc.Bindings = Bindings ?? new Dictionary<string, List<string>>();
            plc.Repositories = Repositories ?? new List<string>();
            plc.Patches = Patches ?? new ConfigPatches();
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
            plcproj_path = $"{_rootpath}\\{slnFolder}\\{plc.ProjectName}\\{Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{_rootpath}\\{plc.ProjectName}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{_rootpath}\\{plc.ProjectName}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{_rootpath}\\{plc.ProjectName}\\{plc.Name}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            plcproj_path = $"{_rootpath}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcproj_path))
            {
                plc.FilePath = plcproj_path;
                return plcproj_path;
            }

            return null;
        }

        public static String Path(ConfigPlcProject plc)
        {
            FileInfo fi = new FileInfo(plc.FilePath());
            return fi.DirectoryName;
        }

        public static string Namespace(ConfigPlcProject plc)
        {
            if (plc.Namespace != null)
                return plc.Namespace;

            XDocument xdoc = XDocument.Load(GuessFilePath(plc));
            plc.Namespace = xdoc.Elements(Config.TcNs + "Project").Elements(Config.TcNs + "PropertyGroup").Elements(Config.TcNs + "DefaultNamespace").FirstOrDefault()?.Value;

            plc.Namespace = Name;
            return plc.Namespace;
        }

        public static XDocument XDoc(onfigPlcProject plc)
        {
            return XDocument.Load(GuessFilePath(plc));
        } 
}