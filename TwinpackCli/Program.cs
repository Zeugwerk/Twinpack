using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twinpack.Models;

namespace Twinpack
{
    class Program
    {
        [Verb("pull", HelpText = "Downloads packages that are references in .Zeugwerk/config.json to .Zeugwerk/libraries, you can use RepTool.exe to install them into the TwinCAT library repository.")]
        public class PullOptions
        {
            [Option('u', "username", Required = true, Default = null, HelpText = "Username for Twinpack Server")]
            public string Username { get; set; }

            [Option('p', "password", Required = true, Default = null, HelpText = "Password for Twinpack Server")]
            public string Password { get; set; }
        }

        [Verb("push", HelpText = "Pushes libraries to a Twinpack Server")]
        public class PushOptions
        {
            [Option('u', "username", Required = true, Default = null, HelpText = "Username for Twinpack Server")]
            public string Username { get; set; }

            [Option('p', "password", Required = true, Default = null, HelpText = "Password for Twinpack Server")]
            public string Password { get; set; }

            [Option('t', "configuration", Required = false, Default = "Debug", HelpText = "Package Configuration (Release, Debug, ...)")]
            public string Configuration { get; set; }

            [Option('t', "target", Required = false, Default = "TC3.1", HelpText = "Package Target")]
            public string Target { get; set; }
            [Option('b', "branch", Required = false, Default = "main", HelpText = "Package Branch")]
            public string Branch { get; set; }

            [Option('m', "notes", Required = false, Default = "", HelpText = "Optional release notes, specific to the file that is uploaded")]
            public string Notes { get; set; }

            [Option('C', "compiled", Required = false, Default = false, HelpText = "The package is a compiled-library")]
            public bool Compiled { get; set; }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static TwinpackServer _twinpackServer = new TwinpackServer();

        static void Login(string username, string password)
        {
            _twinpackServer.LoginAsync(username, password).Wait();
            if (!_twinpackServer.LoggedIn)
                throw new Exception("Login to Twinpack Server failed!");
        }

        public static async Task PushAsync(string configuration, string branch, string target, string notes, bool compiled, string cachePath = null)
        {
            _logger.Info($"Loading configuration file");
            var config = ConfigFactory.Load();

            _logger.Info($"Pushing to Twinpack Server");

            var suffix = compiled ? "compiled-library" : "library";
            var plcs = config.Projects.SelectMany(x => x.Plcs)
                                         .Where(x => x.PlcType == ConfigPlcProject.PlcProjectType.FrameworkLibrary ||
                                                x.PlcType == ConfigPlcProject.PlcProjectType.Library);
            // check if all requested files are present
            foreach (var plc in plcs)
            {
                var fileName = $@"{cachePath ?? TwinpackServer.DefaultLibraryCachePath}\{target}\{plc.Name}_{plc.Version}.{suffix}";
                if (!File.Exists(fileName))
                    throw new LibraryNotFoundException(plc.Name, plc.Version, $"Could not find library file '{fileName}'");

                if (!string.IsNullOrEmpty(plc.LicenseFile) && !File.Exists(plc.LicenseFile))
                    _logger.Warn($"Could not find license file '{plc.LicenseFile}'");
            }

            var exceptions = new List<Exception>();
            foreach (var plc in plcs)
            {
                try
                {
                    suffix = compiled ? "compiled-library" : "library";
                    string binary = Convert.ToBase64String(File.ReadAllBytes($@"{cachePath ?? TwinpackServer.DefaultLibraryCachePath}\{target}\{plc.Name}_{plc.Version}.{suffix}"));
                    string licenseBinary = (!File.Exists(plc.LicenseFile) || string.IsNullOrEmpty(plc.LicenseFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.LicenseFile));
                    string licenseTmcBinary = (!File.Exists(plc.LicenseTmcFile) || string.IsNullOrEmpty(plc.LicenseTmcFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.LicenseTmcFile));
                    string iconBinary = (!File.Exists(plc.IconFile) || string.IsNullOrEmpty(plc.IconFile)) ? null : Convert.ToBase64String(File.ReadAllBytes(plc.IconFile));

                    var packageVersion = new PackageVersionPostRequest()
                    {
                        Name = plc.Name,
                        Version = plc.Version,
                        Target = target,
                        License = plc.License,
                        Description = plc.Description,
                        DistributorName = plc.DistributorName,
                        Authors = plc.Authors,
                        Entitlement = plc.Entitlement,
                        ProjectUrl = plc.ProjectUrl,
                        DisplayName = plc.DisplayName,
                        Branch = branch,
                        Configuration = configuration,
                        Compiled = compiled ? 1 : 0,
                        Notes = notes,
                        IconFilename = Path.GetFileName(plc.IconFile),
                        IconBinary = iconBinary,
                        LicenseBinary = licenseBinary,
                        LicenseTmcBinary = licenseTmcBinary,
                        Binary = binary,
                        Dependencies = plc.Packages?.Select(x => new PackageVersionDependencyPostRequest
                        {
                            Repository = x.Repository,
                            DistributorName = x.DistributorName,
                            Name = x.Name,
                            Version = x.Version,
                            Branch = x.Branch,
                            Target = x.Target,
                            Configuration = x.Configuration
                        })
                    };

                    await _twinpackServer.PostPackageVersionAsync(packageVersion);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"{plc.Name}: {ex.Message}");
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Any())
            {
                throw new Exceptions.PostException($"Pushing to Twinpack Server failed for {exceptions.Count()} packages!");
            }
        }

        public static async Task PullAsync(string cachePath = null)
        {
            _logger.Info($"Loading configuration file");
            var config = ConfigFactory.Load();

            _logger.Info($"Pulling from Twinpack Server");
            var plcs = config.Projects.SelectMany(x => x.Plcs);
            var exceptions = new List<Exception>();
            var downloaded = new List<ConfigPlcPackage>();
            foreach (var plc in plcs)
            {
                foreach (var package in plc.Packages ?? new List<ConfigPlcPackage>())
                {
                    if (downloaded.Any(x => x?.Name == package?.Name && x?.Version == package?.Version && x?.Target == package?.Target && x?.Configuration == package?.Configuration && x?.Branch == package?.Branch))
                        continue;

                    try
                    {
                        var pv = await _twinpackServer.GetPackageVersionAsync(package.DistributorName, package.Name, package.Version, package.Configuration, package.Branch, package.Target, true, cachePath: cachePath);
                        downloaded.Add(package);

                        if (pv?.PackageVersionId == null)
                            throw new Exceptions.GetException("Package not available");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"{package.Name}: {ex.Message}");
                        exceptions.Add(ex);
                    }
                }
            }

            if (exceptions.Any())
            {
                throw new Exceptions.GetException($"Pulling for Twinpack Server failed for {exceptions.Count()} dependencies!");
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            LogManager.Setup();

            try
            {
                return Parser.Default.ParseArguments<PullOptions, PushOptions>(args)
                    .MapResult(
                    (PullOptions opts) =>
                    {
                        Login(opts.Username, opts.Password);
                        PullAsync(opts.Configuration, opts.Branch, opts.Target).Wait();
                        return 0;
                    },
                    (PushOptions opts) =>
                    {
                        Login(opts.Username, opts.Password);
                        PushAsync(opts.Configuration, opts.Branch, opts.Target, opts.Notes, opts.Compiled).Wait();
                        return 0;
                    },
                    errs => 1);
            }
            catch(Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
            finally
            {
            }
        }
    }
}
