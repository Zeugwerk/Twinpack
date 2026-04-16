using System.Threading;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Protocol;

namespace Twinpack.Application
{
    /// <summary>
    /// Shared inputs for static package orchestration helpers on <see cref="TwinpackService"/> (config, automation, servers, cancellation).
    /// </summary>
    internal readonly struct PackageOperationContext
    {
        public PackageOperationContext(Config config, IAutomationInterface automationInterface, IPackageServerCollection packageServers, CancellationToken cancellationToken = default)
        {
            Config = config;
            AutomationInterface = automationInterface;
            PackageServers = packageServers;
            CancellationToken = cancellationToken;
        }

        public Config Config { get; }

        public IAutomationInterface AutomationInterface { get; }

        public IPackageServerCollection PackageServers { get; }

        public CancellationToken CancellationToken { get; }
    }
}
