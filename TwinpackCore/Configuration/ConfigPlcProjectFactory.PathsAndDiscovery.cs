using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NLog;
using Twinpack.Application;
using Twinpack.Models;
using Twinpack.Protocol;
using Twinpack.Protocol.Api;

namespace Twinpack.Configuration
{
    public partial class ConfigPlcProjectFactory
    {
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

            if (string.IsNullOrEmpty(plc.RootPath))
                return null;

            string slnFolder = new DirectoryInfo(plc.RootPath).Name;
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(plc.ProjectName) && !string.IsNullOrEmpty(plc.Name))
            {
                candidates.Add(System.IO.Path.Combine(plc.RootPath, plc.ProjectName, $"{plc.Name}.plcproj"));
                candidates.Add(System.IO.Path.Combine(plc.RootPath, slnFolder, plc.ProjectName, $"{plc.Name}.plcproj"));
                candidates.Add(System.IO.Path.Combine(plc.RootPath, plc.ProjectName, plc.Name, $"{plc.Name}.plcproj"));
                candidates.Add(System.IO.Path.Combine(plc.RootPath, plc.ProjectName, plc.Name, plc.Name, $"{plc.Name}.plcproj"));
            }

            if (!string.IsNullOrEmpty(plc.ProjectName))
                candidates.Add(System.IO.Path.Combine(plc.RootPath, $"{plc.ProjectName}.plcproj"));

            if (!string.IsNullOrEmpty(plc.Name))
                candidates.Add(System.IO.Path.Combine(plc.RootPath, plc.Name, $"{plc.Name}.plcproj"));

            foreach (var candidate in candidates)
            {
                var plcprojPath = System.IO.Path.GetFullPath(candidate);
                if (!File.Exists(plcprojPath))
                    continue;
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
                plc.FilePath = System.IO.Path.Combine(cachePath ?? TwinpackServer.DefaultLibraryCachePath, target, $"{plc.Name}_{plc.Version}.{suffix}");
                if (!File.Exists(plc.FilePath))
                    throw new Exceptions.LibraryNotFoundException(plc.Name, plc.Version, $"Could not find library file '{plc.FilePath}'");

                if (!string.IsNullOrEmpty(plc.LicenseFile) && !File.Exists(plc.LicenseFile))
                    _logger.Warn($"Could not find license file '{plc.LicenseFile}'");

                yield return plc;
            }
        }

        public static IEnumerable<ConfigPlcProject> PlcProjectsFromPath(string rootPath, IPackageServerCollection packageServers)
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
                    FilePath = libraryFile,
                };

                foreach (var dependency in libraryInfo.Dependencies.Where(x => x.Version == "*" || Version.TryParse(x.Version, out _) == true))
                {
                    dependency.Version = dependency.Version == "*" ? null : dependency.Version;
                    PublishedPackageVersion resolvedDependency = null;
                    foreach (var depPackageServer in packageServers.Where(x => x.Connected))
                    {
                        if (resolvedDependency != null)
                            break;

                        resolvedDependency = depPackageServer.ResolvePackageVersionAsync(new PackageReferenceKey { DistributorName = dependency.DistributorName, Name = dependency.Name, Version = dependency.Version }, null, null, null).GetAwaiter().GetResult();
                        if (resolvedDependency.Name != null && (resolvedDependency.Version == dependency.Version || dependency.Version == null))
                        {
                            _logger.Info($"Dependency '{dependency.Name}' (distributor: {dependency.DistributorName}, version: {dependency.Version}) located on {depPackageServer.UrlBase}");
                            plc.Packages = plc.Packages.Append(
                                new PlcPackageReference()
                                {
                                    Name = resolvedDependency.Name,
                                    DistributorName = resolvedDependency.DistributorName,
                                    Version = resolvedDependency.Version,
                                    Configuration = resolvedDependency.Configuration,
                                    Branch = resolvedDependency.Branch,
                                    Target = resolvedDependency.Target
                                }).ToList();
                        }
                    }

                    if (resolvedDependency == null)
                    {
                        _logger.Info($"Dependency '{dependency.Name}' (distributor: {dependency.DistributorName}, version: {dependency.Version})");
                        plc.Packages = plc.Packages.Append(
                            new PlcPackageReference()
                            {
                                Name = dependency.Name,
                                DistributorName = dependency.DistributorName,
                                Version = dependency.Version
                            }).ToList();
                    }
                }

                yield return plc;
            }
        }
    }
}
