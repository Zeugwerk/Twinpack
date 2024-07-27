using NLog;
using System;
using System.Threading.Tasks;


namespace Twinpack.Commands
{
    public abstract class Command
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        protected Protocol.TwinpackServer _twinpackServer = new Protocol.TwinpackServer();

        protected async Task LoginAsync(string username, string password)
        {
            // no need to login without credentials
            if (username == null || password == null)
                return;

            await _twinpackServer.LoginAsync(username, password);
            if (!_twinpackServer.LoggedIn)
                throw new Exception("Login to Twinpack Server failed!");
        }

        public abstract Task<int> ExecuteAsync();
    }
}
