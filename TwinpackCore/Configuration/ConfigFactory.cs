using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NLog;
using Twinpack.Models;

#if !NETSTANDARD2_1_OR_GREATER
using EnvDTE;
using TCatSysManagerLib;
#endif

namespace Twinpack.Configuration
{
    public class ConfigFactory
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public const string DefaultRepository = "https://framework.zeugwerk.dev/Distribution";
        
        public static readonly string ZeugwerkVendorName = "Zeugwerk GmbH";
        public static readonly List<string> DefaultLocations = new List<String> { $@".Zeugwerk\", $@"" };
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

                    config = new Config();

                    try
                    {
                        config = JsonSerializer.Deserialize<Config>(File.ReadAllText($@"{path}\{p}config.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (string.IsNullOrEmpty(config.Solution))
                        {
                            _logger.Warn($@"Failed to parse '{path}\{p}config.json'");
                            continue;
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.Trace(ex);
                        _logger.Warn($@"Failed to parse '{path}\{p}config.json'");
                        continue;
                    }

                    usedPrefix = p;
                    config.WorkingDirectory = Path.GetDirectoryName($@"{path}\.Zeugwerk");
                    config.FilePath = $@"{path}\{p}config.json";

                    var solution = new Models.Solution();
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
#if !NETSTANDARD2_1_OR_GREATER
        public static Task<Config> CreateFromSolutionAsync(EnvDTE.Solution solution, Protocol.IPackageServer packageServer, IEnumerable<ConfigPlcProject.PlcProjectType> plcTypeFilter = null, CancellationToken cancellationToken = default)
        {
            return CreateFromSolutionAsync(solution, new List<Protocol.IPackageServer> { packageServer }, plcTypeFilter, cancellationToken);
        }

        public static async Task<Config> CreateFromSolutionAsync(EnvDTE.Solution solution, IEnumerable<Protocol.IPackageServer> packageServers, IEnumerable<ConfigPlcProject.PlcProjectType> plcTypeFilter = null, CancellationToken cancellationToken = default)
        {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread

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

#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

            return config;
        }
#endif

        public static async Task<Config> CreateFromSolutionFileAsync(string path=".", bool continueWithoutSolution=false, IEnumerable<Protocol.IPackageServer> packageServers=null, IEnumerable<ConfigPlcProject.PlcProjectType> plcTypeFilter=null, CancellationToken cancellationToken = default)
        {
            packageServers = packageServers == null ? new List<Protocol.IPackageServer>() : packageServers;

            Config config = new Config();
            var solutions = Directory.GetFiles(path, "*.sln", SearchOption.AllDirectories);

            if (solutions.Count() > 1)
                _logger.Warn("There is more than 1 solution present in the current directory, only the first one is considered!");
            else if (solutions.Any() == false && continueWithoutSolution == false)
                return null;

            config.Fileversion = 1;
            config.WorkingDirectory = path;

            Models.Solution solution = null;
            if (solutions.Any())
            {
                config.Solution = Path.GetFileName(solutions.First());
                config.FilePath = Path.GetDirectoryName(solutions.First()) + @"\.Zeugwerk\config.json";

                solution = Models.Solution.LoadFromFile(solutions.First());
            }
            else if(continueWithoutSolution)
            {
                config.FilePath = $@"{Environment.CurrentDirectory}\.Zeugwerk\config.json";

                var tsprojs = Directory.GetFiles(path, "*.tsproj", SearchOption.AllDirectories);
                if (tsprojs.Any())
                {
                    var name = Path.GetFileNameWithoutExtension(tsprojs.First());
                    solution = new Models.Solution(name, new List<Models.Project> { new Models.Project(name, tsprojs.First()) });
                }

                if (solution == null)
                {
                    var plcprojs = Directory.GetFiles(path, "*.plcproj", SearchOption.AllDirectories);
                    if (plcprojs.Any())
                    {
                        var name = Path.GetFileNameWithoutExtension(plcprojs.First());
                        var project = new Models.Project(name, plcprojs.Select(x => new Plc(Path.GetFileNameWithoutExtension(x), x)).ToList());
                        solution = new Models.Solution(name, new List<Models.Project> { project });
                    }
                }
            }

            if (solution?.Projects?.FirstOrDefault()?.Plcs?.FirstOrDefault() == null)
                return null;

            foreach (var project in solution.Projects)
            {
                var projectConfig = new ConfigProject();
                projectConfig.Name = project.Name;
                projectConfig.Plcs = new List<ConfigPlcProject>();

                foreach (var plc in project.Plcs)
                {
                    var plcConfig = await ConfigPlcProjectFactory.CreateAsync(plc.FilePath, packageServers, cancellationToken);
                    plcConfig.ProjectName = project.Name;

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
                config.Projects.Add(new ConfigProject { Name = plcConfig.ProjectName, Plcs = new List<ConfigPlcProject> { plcConfig } });
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
}
