using NLog;
using System;
using System.Threading.Tasks;


namespace Twinpack.Commands
{
    public abstract class Command
    {
        protected static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        protected Protocol.TwinpackServer _twinpackServer = new Protocol.TwinpackServer();
        protected Protocol.BeckhoffServer _beckhoffServer = new Protocol.BeckhoffServer();

        protected async Task LoginAsync(string username, string password, string beckhoffUsername, string beckhoffPassword)
        {
            await _twinpackServer.LoginAsync(username, password);
            if (!_twinpackServer.Connected)
                throw new Exception("Login to Twinpack Repository failed!");


            if (beckhoffUsername != null && beckhoffPassword != null)
            {
                await _beckhoffServer.LoginAsync(beckhoffUsername, beckhoffPassword);
                if (!_beckhoffServer.Connected)
                    throw new Exception("Login to Beckhoff Repository failed!");
            }
        }

        public abstract Task<int> ExecuteAsync();
    }
}
