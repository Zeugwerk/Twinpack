using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.Caching; // Add a reference to System.Runtime.Caching.dll
using System.Threading;
using System.Threading.Tasks;

namespace Twinpack.Protocol
{
    public class CachedHttpClient : HttpClient
    {
        private readonly ObjectCache _cache;

        public CachedHttpClient() : base()
        {
            _cache = MemoryCache.Default;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default, TimeSpan? cacheDuration = null)
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                    throw new HttpRequestException("No internet connection! Please check your connection.");

                if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
                {
                    return (await base.SendAsync(request, cancellationToken)).EnsureSuccessStatusCode();
                }

                cacheDuration = cacheDuration ?? TimeSpan.FromHours(12);

                var url = request.RequestUri.ToString();
                if (_cache.Contains(url))
                {
                    return _cache.Get(url) as HttpResponseMessage;
                }

                var response = await base.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var cacheItemPolicy = new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.Now.Add((TimeSpan)cacheDuration)
                };
                _cache.Set(url, response, cacheItemPolicy);
                return response;
            }
            catch (TaskCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;

                throw new TimeoutException($"Timed out during request to {request.RequestUri.Scheme}://{request.RequestUri.Host}\nTwinpack Server is unresponsive! There might be no internet connection or the server may be temporarily down! " +
                        "Please check your connection or try again later.");
            }
            finally
            {
            }
        }

        public void Invalidate()
        {
            try
            {
                foreach (var item in _cache)
                    _cache.Remove(item.Key);
            }
            finally
            {
            }
        }
    }
}