using NLog;
using System;
using System.Threading.Tasks;
using Twinpack.Models;
using Twinpack.Protocol.Api;
using System.Threading;
using System.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging.Core;
using NuGet.Versioning;

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
        protected override bool ResolveForeignDependencies { get => false; }
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
            var libraryName = library?.Name ?? "";
            var prefixed = libraryName.StartsWith("TwinCAT.XAE.PLC.Lib.", StringComparison.OrdinalIgnoreCase)
                ? libraryName
                : "TwinCAT.XAE.PLC.Lib." + libraryName;

            var resolved = await base.ResolvePackageVersionAsync(
                new PlcLibrary(library) { Name = prefixed },
                preferredTarget, preferredConfiguration, preferredBranch, cancellationToken);

            // Tc2_Standard is TwinCAT.XAE.PLC.Lib.Tc2_Standard. MC3 is not: the feed id is
            // TwinCAT.XAE.PLC.Lib.Motion.MC3.Tc3_Mc3Base, so the prefixed lookup misses and the
            // reference is filed as a system library instead of a package.
            if (!string.IsNullOrEmpty(resolved?.Name) || string.IsNullOrEmpty(libraryName) ||
                libraryName.StartsWith("TwinCAT.XAE.PLC.Lib.", StringComparison.OrdinalIgnoreCase))
                return resolved;

            var matches = await SearchAsync("", libraryName, 0, 20, cancellationToken);
            var id = matches
                .Select(x => x.Identity.Id)
                .Where(x => x.EndsWith("." + libraryName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.StartsWith("TwinCAT.XAE.PLC.Lib.", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (id == null)
                return resolved;

            return await base.ResolvePackageVersionAsync(
                new PlcLibrary(library) { Name = id },
                preferredTarget, preferredConfiguration, preferredBranch, cancellationToken);
        }

#if !TWINPACK_HEADLESS
        protected override async Task<System.Windows.Media.Imaging.BitmapImage> GetPackageIconAsync(PackageIdentity identity, CancellationToken cancellationToken)
        {
            // Beckhoff Packages come without Icons
            return null;
        }
#endif
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        protected override async Task<string> EvaluateTitleAsync(IPackageSearchMetadata package, CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            // The library TwinCAT installs is the last segment of the package id
            // (TwinCAT.XAE.PLC.Lib.Motion.MC3.Tc3_Mc3Base -> Tc3_Mc3Base). The word after the
            // "Library" tag is that name for the common packages, and a product family (MC3) when it is not.
            var id = package.Identity?.Id ?? "";
            var lastSegment = id.Split('.').LastOrDefault();
            if (string.IsNullOrEmpty(lastSegment))
                return id;

            var tags = (package.Tags ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var libraryIdx = Array.IndexOf(tags, "Library");
            if (libraryIdx >= 0 && libraryIdx + 1 < tags.Length &&
                string.Equals(tags[libraryIdx + 1], lastSegment, StringComparison.OrdinalIgnoreCase))
                return tags[libraryIdx + 1];

            return lastSegment;
        }

        protected override string EvaluateVersion(NuGetVersion version)
        {
            return version?.Version?.ToString();
        }

        protected override int EvaluateCompiled(string tags)
        {
            return 1;
        }
    }
}
