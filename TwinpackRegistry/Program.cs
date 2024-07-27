using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
            [Option('u', "username", Required = false, Default = null, HelpText = "Username for Twinpack Server")]
            public string Username { get; set; }

            [Option('p', "password", Required = false, Default = null, HelpText = "Password for Twinpack Server")]
            public string Password { get; set; }

            [Option('r', "owner", Required = false, Default = "Zeugwerk", HelpText = "")]
            public string RegistryOwner { get; set; }

            [Option('r', "name", Required = false, Default = "Twinpack-Registry", HelpText = "")]
            public string RegistryName { get; set; }

            [Option('d', "dry-run", Required = false, Default = false, HelpText = "")]
            public bool DryRun { get; set; }

            [Option('D', "dump", Required = false, Default = false, HelpText = "")]
            public bool Dump { get; set; }

            [Option('t', "token", Required = false, Default = false, HelpText = "")]
            public string Token { get; set; }
        }

        [Verb("update-downloads", HelpText = "Downloads packages that are references in .Zeugwerk/config.json to .Zeugwerk/libraries, you can use RepTool.exe to install them into the TwinCAT library repository.")]
        public class UpdateDownloadsOptions
        {
            [Option('u', "username", Required = false, Default = null, HelpText = "Username for Twinpack Server")]
            public string Username { get; set; }

            [Option('p', "password", Required = false, Default = null, HelpText = "Password for Twinpack Server")]
            public string Password { get; set; }

            [Option('r', "owner", Required = false, Default = "Zeugwerk", HelpText = "")]
            public string RegistryOwner { get; set; }

            [Option('r', "name", Required = false, Default = "Twinpack-Registry", HelpText = "")]
            public string RegistryName { get; set; }

            [Option('d', "dry-run", Required = false, Default = false, HelpText = "")]
            public bool DryRun { get; set; }

            [Option('t', "token", Required = false, Default = false, HelpText = "")]
            public string Token { get; set; }
        }

        [Verb("dump", HelpText = "")]
        public class DumpOptions
        {
            [Option('p', "path", Required = false, Default = "Twinpack-Registry", HelpText = "")]
            public string Path { get; set; }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static Protocol.TwinpackServer _twinpackServer = new Protocol.TwinpackServer();

        static void Login(string username, string password)
        {
            // no need to login without credentials
            if (username == null || password == null)
                return;

            _twinpackServer.LoginAsync(username, password).Wait();
            if (!_twinpackServer.LoggedIn)
                throw new Exception("Login to Twinpack Server failed!");
        }

        [STAThread]
        static int Main(string[] args)
        {
            _logger.Info("-------------------------------------------------------------------------");
            _logger.Info($"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name} {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            _logger.Info("-------------------------------------------------------------------------");

            LogManager.Setup();
            var watch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                return Parser.Default.ParseArguments<PullOptions, UpdateDownloadsOptions, DumpOptions>(args)
                    .MapResult(
                    (PullOptions opts) =>
                    {
                        _logger.Info(">>> twinpack-registry:pull");

                        Login(opts.Username, opts.Password);
                        _logger.Info(new string('-', 3) + $" download");
                        new TwinpackRegistry(_twinpackServer).DownloadAsync(opts.RegistryOwner, opts.RegistryName, token: opts.Token).GetAwaiter().GetResult();

                        if(!opts.DryRun)
                        {
                            _logger.Info(new string('-', 3) + $" push");
                            _twinpackServer.PushAsync(TwinpackUtils.PlcProjectsFromConfig(compiled: false, target: "TC3.1"), "Release", "main", "TC3.1", null, false).GetAwaiter().GetResult();
                        }

                        return 0;
                    },
                    (UpdateDownloadsOptions opts) =>
                    {
                        _logger.Info(">>> twinpack-registry:update-downloads");

                        Login(opts.Username, opts.Password);
                        new TwinpackRegistry(_twinpackServer).UpdateDownloadsAsync(opts.RegistryOwner, opts.RegistryName, token: opts.Token, dryRun: opts.DryRun).GetAwaiter().GetResult();

                        return 0;
                    },
                    (DumpOptions opts) =>
                    {
                        _logger.Info(">>> twinpack-registry:dump");

                        foreach (var file in Directory.GetFiles(opts.Path))
                        {
                            try
                            {
                                using (var memoryStream = new MemoryStream(File.ReadAllBytes(file)))
                                using (var zipArchive = new ZipArchive(memoryStream))
                                {
                                    var libraryInfo = LibraryReader.Read(File.ReadAllBytes(file), dumpFilenamePrefix: file);
                                }
                            } catch(Exception) { }
                        }
                        return 0;
                    },
                    errs => 1);
            }
            catch (Exception ex)
            {
               _logger.Error(ex);

                _logger.Info("-------------------------------------------------------------------------");
                _logger.Info("FAILED");
                _logger.Info("-------------------------------------------------------------------------");

                throw;
            }
            finally
            {
                watch.Stop();
                TimeSpan ts = watch.Elapsed;
                string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

                _logger.Info($"Total time: {elapsedTime}");
                _logger.Info($"Finished at: {DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")}");
                _logger.Info("-------------------------------------------------------------------------");
            }
        }
    }
}
