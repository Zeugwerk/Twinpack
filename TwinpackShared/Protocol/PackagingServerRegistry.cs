using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Twinpack.Protocol
{
    public class PackagingServerRegistry
    {
        static string _filePath = @"%APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json";
        static List<IPackagingServerFactory> _factories;
        static PackageServerCollection _servers;

        public static async Task InitializeAsync(bool useDefaults=false)
        {
            _filePath = Environment.ExpandEnvironmentVariables(_filePath);
            _factories = new List<IPackagingServerFactory>() { new NativePackagingServerFactory(), new NugetPackagingServerFactory(), new BeckhoffPackagingServerFactory() };
            _servers = new PackageServerCollection();

            if(useDefaults)
            {
                await AddServerAsync("Twinpack Repository", "twinpack.dev", TwinpackServer.DefaultUrlBase);
                await AddServerAsync("Beckhoff Repository", "public.tcpkg.beckhoff-cloud.com (stable)", "https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable");
            }
            else
            {
                try
                {
                    if (!File.Exists(_filePath))
                        throw new FileNotFoundException("Configuration file not found");

                    var sourceRepositories = JsonSerializer.Deserialize<Models.SourceRepositories>(File.ReadAllText(_filePath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    foreach (var server in sourceRepositories.PackagingServers)
                        await AddServerAsync(server.ServerType, server.Name, server.Url);
                }
                catch
                {
                    await AddServerAsync("Twinpack Repository", "twinpack.dev", TwinpackServer.DefaultUrlBase);
                    await AddServerAsync("Beckhoff Repository", "public.tcpkg.beckhoff-cloud.com (stable)", "https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable");
                }
            }
        }
        public static IEnumerable<string> ServerTypes { get { return _factories.Select(x => x.ServerType); } }
        public static PackageServerCollection Servers { get { return _servers; } }
        public static IPackageServer CreateServer(string type, string name, string uri)
        {
            var factory = _factories.Where(x => x.ServerType == type).FirstOrDefault();
            if (factory == null)
                throw new Exceptions.PackageServerTypeNotFoundException($"Generator for package server with type {type} not found");

            return factory.Create(name, uri);
        }

        public static async Task<IPackageServer> AddServerAsync(string type, string name, string uri)
        {
            var server = CreateServer(type, name, uri);
            var auth = new Authentication(server);
            await auth.LoginAsync(onlyTry: true);
            _servers.Add(server);
            return server;
        }

        public static void Save()
        {
            var sourceRepositories = new Models.SourceRepositories();
            Servers.ForEach(x =>
                sourceRepositories.PackagingServers.Add(new Models.PackagingServer() { Name = x.Name, ServerType = x.ServerType, Url = x.UrlBase }));

            if (!Directory.Exists(Path.GetDirectoryName(_filePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

            File.WriteAllText(_filePath, JsonSerializer.Serialize(sourceRepositories, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
