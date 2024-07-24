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
        protected static PackageServerCollection _packageServers;

        public abstract int Execute();

        protected void Login(string username, string password)
        {
            PackagingServerRegistry.InitializeAsync().GetAwaiter().GetResult();

            _packageServers = PackagingServerRegistry.Servers;

            foreach (var twinpackServer in _packageServers.Where(x => x as TwinpackServer != null).Select(x => x as TwinpackServer))
            {
                twinpackServer.LoginAsync(username, password).Wait();
                if (!twinpackServer.LoggedIn && (!string.IsNullOrEmpty(username) || !string.IsNullOrEmpty(password)))
                    throw new Exception("Login to Twinpack Server failed!");

                if (!twinpackServer.Connected)
                    throw new Exception("Connection to Twinpack Server failed!");
            }
        }
    }
}
