using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;

            var sourceUri = "https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable/";
            var packageSource = new PackageSource(sourceUri)
            {
                Credentials = new PackageSourceCredential(sourceUri, "stefan.besler@swarovski.com", "RjZxsPg45qpRc5zCcKf2UD", true, null)
            };
            SourceCacheContext cache = new SourceCacheContext();

            SourceRepository repository = Repository.Factory.GetCoreV3(packageSource);
            var resource = await repository.GetResourceAsync<PackageSearchResource>();

            IEnumerable<IPackageSearchMetadata> packages = await resource.SearchAsync("TwinCAT.XAE.PLC.Lib", new SearchFilter(includePrerelease: false), 0, 100, logger, default);

            foreach (IPackageSearchMetadata package in packages)
            {
                Console.WriteLine($"{package.Identity.Id} Version={package.Identity.Version}");
                Console.WriteLine($"  Tags: {package.Tags}");
                Console.WriteLine($"  Desc: {package.Description}");
            }
        }
    }
}
