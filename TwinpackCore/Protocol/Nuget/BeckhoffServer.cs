using NLog;
using System.Threading.Tasks;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using System.Threading;

namespace Twinpack.Protocol
{
    class BeckhoffPackagingServerFactory : IPackagingServerFactory
    {
        public IPackageServer Create(string name, string uri)
        {
            return new BeckhoffServer(name, uri);
        }

        public string ServerType { get; } = "Beckhoff Repository";
    }

    public class BeckhoffServer : NugetServer, IPackageServer
    {
        public const string DefaultUrlBase = "https://public.tcpkg.beckhoff-cloud.com/api/v1/feeds/stable/";

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        public new string ServerType { get; } = "Beckhoff Repository";
        protected override string SearchPrefix { get => "tags:library "; }
        public override string UrlRegister
        {
            get => "https://www.beckhoff.com/en-en/mybeckhoff-registration/index.aspx";
        }
        protected override string IconUrl { get => "https://twinpack.dev/Icons/beckhoff.png"; }

        public BeckhoffServer(string name = "", string url = DefaultUrlBase) : base(name, url)
        {

        }

        public override async Task<PackageVersionGetResponse> ResolvePackageVersionAsync(PlcLibrary library, string preferredTarget = null, string preferredConfiguration = null, string preferredBranch = null, CancellationToken cancellationToken = default)
        {
            if(!library.Name.StartsWith("TwinCAT.XAE.PLC.Lib."))
                library.Name = "TwinCAT.XAE.PLC.Lib." + library.Name;

            return await base.ResolvePackageVersionAsync(library, preferredTarget, preferredConfiguration, preferredBranch, cancellationToken);
        }

        protected override int EvaluateCompiled(string tags)
        {
            return 1;
        }
    }
}
