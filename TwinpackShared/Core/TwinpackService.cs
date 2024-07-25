using NLog;
using NLog.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Caching;
using Twinpack.Exceptions;
using Twinpack.Models;
using Twinpack.Protocol;
using static System.Net.Mime.MediaTypeNames;

namespace Twinpack.Core
{
    public class TwinpackService
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private List<CatalogItem> _availablePackageCache = new List<CatalogItem>();
        private PackageServerCollection _packageServers;
        private IAsyncEnumerator<CatalogItem> _availablePackagesIt;
        private string _searchTerm;

        public TwinpackService(PackageServerCollection packageServers)
        {
            _packageServers = packageServers;
        }

        public async Task LoginAsync(string username=null, string password=null)
        {
            await _packageServers.LoginAsync(username, password);
        }

        public async Task<IEnumerable<CatalogItem>> RetrieveNextAvailablePackagesAsync(string searchTerm = null, int? maxNewPackages = null, int batchSize = 5, CancellationToken token = default)
        {
            if(_availablePackagesIt == null || _searchTerm != searchTerm)
                _availablePackagesIt = _packageServers.SearchAsync(searchTerm, null, batchSize, token).GetAsyncEnumerator();

            _searchTerm = searchTerm;
            var maxPackages = _availablePackageCache.Count + maxNewPackages;
            while ((maxNewPackages == null || _availablePackageCache.Count < maxPackages) && await _availablePackagesIt.MoveNextAsync())
            {
                CatalogItem item = _availablePackagesIt.Current;

                // only add if we don't have this package cached already
                if(!_availablePackageCache.Any(x => item.Name == x.Name))
                    _availablePackageCache.Add(item);
            }

            return _availablePackageCache
            .Where(x =>
                        searchTerm == null ||
                        x.DisplayName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.DistributorName?.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
                        x.Name.IndexOf(searchTerm, StringComparison.InvariantCultureIgnoreCase) >= 0)
                    ;
        }
    }
}
