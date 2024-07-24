using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Protocol;

namespace Twinpack.Commands
{
    public abstract class Command
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        protected static PackageServerCollection _packageServers;

        public abstract int Execute();

        protected async Task LoginAsync(string username=null, string password=null)
        {
            await PackagingServerRegistry.InitializeAsync();

            _packageServers = PackagingServerRegistry.Servers;
            foreach(var packageServer in _packageServers)
            {
                try
                {
                    await packageServer.LoginAsync(username, password);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex.Message);
                    _logger.Trace(ex);
                }
            }
        }
    }
}
