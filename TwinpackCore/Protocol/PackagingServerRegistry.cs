using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Twinpack.Core;
using Twinpack.Exceptions;

namespace Twinpack.Protocol
{
    public class PackagingServerRegistry
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static readonly List<string> _filePaths = new List<string> {
            @".\sourceRepositories.json",
            @"%APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json"
        };

        static List<IPackagingServerFactory> _factories = new List<IPackagingServerFactory>();
        static PackageServerCollection _servers = new PackageServerCollection();

        private static IEnumerable<string> FilePaths { get => _filePaths.Select(x => Environment.ExpandEnvironmentVariables(x)); }

        private static string FilePath
        { 
            get
            {
                foreach (var filePath in FilePaths)
                {
                    if (File.Exists(filePath))
                        return filePath;

                }

                return null;
            }
        }
        public static async Task InitializeAsync(bool useDefaults=false, bool login=true)
        {
            _factories = new List<IPackagingServerFactory>() { new NativePackagingServerFactory(), new NugetPackagingServerFactory(), new BeckhoffPackagingServerFactory() };
            _servers = new PackageServerCollection();

            if(useDefaults)
            {
                await AddServerAsync("Twinpack Repository", "twinpack.dev", TwinpackServer.DefaultUrlBase, login: false);
                await AddServerAsync("Beckhoff Repository", "public.tcpkg.beckhoff-cloud.com (stable)", "https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable", login: false);
                Save();
            }
            else
            {
                try
                {
                    if (FilePath == null)
                        throw new FileNotFoundException($"No configuration file not found (searched in {string.Join(",", FilePaths)})");

                    var sourceRepositories = JsonSerializer.Deserialize<Models.SourceRepositories>(File.ReadAllText(FilePath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    foreach (var server in sourceRepositories.PackagingServers)
                        await AddServerAsync(server.ServerType, server.Name, server.Url, login);
                }
                catch(FileNotFoundException)
                {
                    throw;
                }
                catch(Exception ex)
                {
                    _logger.Trace(ex);
                    _logger.Warn($"Failed to load configuration, using default repositories");

                    if(useDefaults)
                    {
                        await AddServerAsync("Twinpack Repository", "twinpack.dev", TwinpackServer.DefaultUrlBase);
                        await AddServerAsync("Beckhoff Repository", "public.tcpkg.beckhoff-cloud.com (stable)", "https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable");
                        Save();
                    }
                }
            }
        }
        public static IEnumerable<string> ServerTypes { get { return _factories.Select(x => x.ServerType); } }
        public static PackageServerCollection Servers { get => _servers; }
        public static IPackageServer CreateServer(string type, string name, string uri)
        {
            if (string.IsNullOrEmpty(name))
                throw new PackageServerTypeException("Invalid package server type!");

            var factory = _factories.Where(x => x.ServerType == type).FirstOrDefault();
            if (factory == null)
                throw new PackageServerTypeException($"Factory for package server with type '{type}' not found (use one of {string.Join(",", ServerTypes.Select(x => "'" + x + "'"))})");

            return factory.Create(name, uri);
        }

        public static async Task<IPackageServer> AddServerAsync(string type, string name, string uri, bool login=true)
        {
            var server = CreateServer(type, name, uri);
            if(login)
            {
                var auth = new Authentication(server);
                await auth.LoginAsync(onlyTry: true);
            }

            _servers.Add(server);
            return server;
        }

        public static void Save()
        {
            var sourceRepositories = new Models.SourceRepositories();
            Servers.ForEach(x =>
                sourceRepositories.PackagingServers.Add(new Models.PackagingServer() { Name = x.Name, ServerType = x.ServerType, Url = x.UrlBase }));

            var filePath = FilePath;
            if(filePath == null)
                filePath = FilePaths.Last();

            var dirPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            File.WriteAllText(filePath, JsonSerializer.Serialize(sourceRepositories, new JsonSerializerOptions { WriteIndented = true }));
            _logger.Info($"Saved configuration in '{filePath}'");
        }

        public static async Task PurgeAsync()
        {
            if (FilePath != null)
            {
                foreach (var packageServer in Servers)
                    await packageServer.LogoutAsync();

                Servers.Clear();
                File.Delete(FilePath);
            }

        }
    }
}
