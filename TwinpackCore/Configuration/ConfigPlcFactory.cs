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
using EnvDTE;
using NLog;
using NuGet.Common;
using TCatSysManagerLib;
using Twinpack.Models;

namespace Twinpack.Configuration
{
    public class ConfigPlcProjectFactory
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static XNamespace TcNs = "http://schemas.microsoft.com/developer/msbuild/2003";

        public static ConfigPlcProject MapPlcConfigToPlcProj(Config config, string plcName)
        {
            return config.Projects.SelectMany(x => x.Plcs).FirstOrDefault(x => x.Name == plcName);
        }

        public static Task<ConfigPlcProject> CreateAsync(EnvDTE.Solution solution, EnvDTE.Project prj, Protocol.IPackageServer packageServer, CancellationToken cancellationToken = default)
        {
            return CreateAsync(solution, prj, new List<Protocol.IPackageServer> { packageServer }, cancellationToken);
        }

        public static async Task<ConfigPlcProject> CreateAsync(EnvDTE.Solution solution, EnvDTE.Project prj, IEnumerable<Protocol.IPackageServer> packageServers, CancellationToken cancellationToken = default)
        {
#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread

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

#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread

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

            AddPlcLibraryOptions ParseOptions(XElement element, bool isLibraryReference)
            {
                var ret = new AddPlcLibraryOptions
                {
                    LibraryReference = isLibraryReference,
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

            /*
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
                        Options = ParseOptions(g.Parent, false)
                    });
                }
            }
            */

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
                        Options = ParseOptions(g.Parent, false)
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
                    references.Add(new PlcLibrary
                    {
                        Name = match.Groups[1].Value.Trim(),
                        Version = version == "*" ? null : version,
                        DistributorName = match.Groups[3].Value.Trim(),
                        Options = ParseOptions(g, true)
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

                foreach (var packageServer in packageServers)
                {
                    try
                    {
                        var packageVersion = await packageServer.ResolvePackageVersionAsync(r, cancellationToken: cancellationToken);
                        if (isPackage = packageVersion?.Name != null && packageVersion?.DistributorName != null)
                        {
                            packages.Add(new ConfigPlcPackage
                            {
                                DistributorName = packageVersion.DistributorName,
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
                else if (!isPackage)
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
                plc.References["*"].Add($"{r.Name}={r.Version ?? "*"}");
            }

         

            plc.Type = GuessPlcType(plc).ToString();return plc;
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

        public static string GuessFilePath(ConfigPlcProject plc)
        {
            // todo: parse sln, ts(p)proj and xti to get the path of the PLC instead of guessing
            if (!string.IsNullOrEmpty(plc.FilePath))
                return plc.FilePath;

            var plcprojPath = $"{plc.RootPath}\\{plc.ProjectName}\\{plc.Name}.plcproj";
            if (File.Exists(plcprojPath))
            {
                plc.FilePath = plcprojPath;
                return plcprojPath;
            }

            string slnFolder = new DirectoryInfo(plc.RootPath).Name;
            plcprojPath = $"{plc.RootPath}\\{slnFolder}\\{plc.ProjectName}\\{plc.Name}.plcproj";
            if (File.Exists(plcprojPath))
            {
                plc.FilePath = plcprojPath;
                return plcprojPath;
            }

            plcprojPath = $"{plc.RootPath}\\{plc.ProjectName}.plcproj";
            if (File.Exists(plcprojPath))
            {
                plc.FilePath = plcprojPath;
                return plcprojPath;
            }

            plcprojPath = $"{plc.RootPath}\\{plc.ProjectName}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcprojPath))
            {
                plc.FilePath = plcprojPath;
                return plcprojPath;
            }

            plcprojPath = $"{plc.RootPath}\\{plc.ProjectName}\\{plc.Name}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcprojPath))
            {
                plc.FilePath = plcprojPath;
                return plcprojPath;
            }

            plcprojPath = $"{plc.RootPath}\\{plc.Name}\\{plc.Name}.plcproj";
            if (File.Exists(plcprojPath))
            {
                plc.FilePath = plcprojPath;
                return plcprojPath;
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
                plc.FilePath = $@"{cachePath ?? Protocol.TwinpackServer.DefaultLibraryCachePath}\{target}\{plc.Name}_{plc.Version}.{suffix}";
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
