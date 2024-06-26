﻿using CommandLine;
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
            [Option('u', "username", Required = false, Default = null, HelpText = "Username for Twinpack Server")]
            public string Username { get; set; }

            [Option('p', "password", Required = false, Default = null, HelpText = "Password for Twinpack Server")]
            public string Password { get; set; }

            [Option('P', "provided", Required = false, Default = false, HelpText = "Also pull packages that are provided by the package definition")]
            public bool Provided { get; set; }
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

            [Option('c', "without-config", Required = false, Default = false, HelpText = "Don't use a config.json file, but use the information where to find libraries from the other arguments")]
            public bool WithoutConfig { get; set; }

            [Option('p', "library-path", Required = false, Default = ".", HelpText = "Only valid when without-config is used, path where .library files are located")]
            public string LibraryPath { get; set; }

            [Option('d', "skip-duplicate", Required = false, Default = false, HelpText = "If a package and version already exists, skip it and continue with the next package in the push, if any")]
            public bool SkipDuplicate { get; set; }
        }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static Protocol.TwinpackServer _twinpackServer = new Protocol.TwinpackServer();

        static void Login(string username, string password)
        {
            // no need to login without credentials
            if(username == null || password == null)
                return;
            
            _twinpackServer.LoginAsync(username, password).Wait();
            if (!_twinpackServer.LoggedIn)
                throw new Exception("Login to Twinpack Server failed!");
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
                        var config = ConfigFactory.Load();
                        _twinpackServer.PullAsync(config, skipInternalPackages: !opts.Provided).GetAwaiter().GetResult();
                        return 0;
                    },
                    (PushOptions opts) =>
                    {
                        Login(opts.Username, opts.Password);

                        _twinpackServer.PushAsync(
                            opts.WithoutConfig ? 
                                TwinpackUtils.PlcProjectsFromPath(opts.LibraryPath) : 
                                TwinpackUtils.PlcProjectsFromConfig(opts.Compiled, opts.Target),
                            opts.Configuration, 
                            opts.Branch, 
                            opts.Target, 
                            opts.Notes, 
                            opts.Compiled,
                            opts.SkipDuplicate).GetAwaiter().GetResult();
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
