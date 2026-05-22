using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Twinpack.Protocol
{
    public class CachedHttpClient : HttpClient
    {
        sealed class CachedHttpEntry
        {
            public byte[] Body { get; set; }
            public HttpStatusCode StatusCode { get; set; }
            public string ContentType { get; set; }
            public Dictionary<string, string[]> ResponseHeaders { get; set; }
        }

        private readonly ObjectCache _cache;

        public CachedHttpClient() : base()
        {
            _cache = MemoryCache.Default;
        }

        static bool ShouldCacheRequest(HttpRequestMessage request)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.IndexOf("/binary", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return true;
        }

        static CachedHttpEntry CreateEntry(HttpResponseMessage response, byte[] bytes)
        {
            var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
                headers[header.Key] = header.Value.ToArray();

            return new CachedHttpEntry
            {
                Body = bytes,
                StatusCode = response.StatusCode,
                ContentType = response.Content?.Headers?.ContentType?.ToString(),
                ResponseHeaders = headers
            };
        }

        static HttpResponseMessage ToResponse(CachedHttpEntry entry, HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(entry.StatusCode)
            {
                Content = new ByteArrayContent(entry.Body ?? Array.Empty<byte>()),
                RequestMessage = request
            };

            if (!string.IsNullOrEmpty(entry.ContentType))
                response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(entry.ContentType);

            if (entry.ResponseHeaders != null)
            {
                foreach (var header in entry.ResponseHeaders)
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return response;
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

                if (!ShouldCacheRequest(request))
                {
                    var uncached = await base.SendAsync(request, cancellationToken);
                    return uncached.EnsureSuccessStatusCode();
                }

                cacheDuration = cacheDuration ?? TimeSpan.FromHours(12);

                var url = request.RequestUri.ToString();
                if (_cache.Contains(url))
                {
                    var cached = _cache.Get(url) as CachedHttpEntry;
                    if (cached != null)
                        return ToResponse(cached, request);
                }

                using (var response = await base.SendAsync(request, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var bytes = response.Content == null
                        ? Array.Empty<byte>()
                        : await response.Content.ReadAsByteArrayAsync();

                    var entry = CreateEntry(response, bytes);

                    var cacheItemPolicy = new CacheItemPolicy
                    {
                        AbsoluteExpiration = DateTimeOffset.Now.Add((TimeSpan)cacheDuration)
                    };
                    _cache.Set(url, entry, cacheItemPolicy);

                    return ToResponse(entry, request);
                }
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;

                throw new TimeoutException($"Timed out during request to {request.RequestUri.Scheme}://{request.RequestUri.Host}\nTwinpack Server is unresponsive! There might be no internet connection or the server may be temporarily down! " +
                        "Please check your connection or try again later.");
            }
        }

        public void Invalidate()
        {
            foreach (var item in _cache)
                _cache.Remove(item.Key);
        }
    }
}
