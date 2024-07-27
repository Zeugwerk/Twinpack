using NLog;
using System.Threading.Tasks;


namespace Twinpack.Commands
{
    public abstract class Command
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        protected Core.TwinpackService _twinpack;
        public abstract Task<int> ExecuteAsync();
    }
}
