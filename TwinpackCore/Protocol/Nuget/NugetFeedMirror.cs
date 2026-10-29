#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Twinpack.Protocol
{
    public sealed class MirrorItem
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Error { get; set; }
    }

    public sealed class MirrorResult
    {
        public bool DryRun { get; set; }
        public List<MirrorItem> Pushed { get; set; } = new List<MirrorItem>();
        public List<MirrorItem> Skipped { get; set; } = new List<MirrorItem>();
        public List<MirrorItem> Failed { get; set; } = new List<MirrorItem>();
    }

    /// <summary>
    /// Copies selected packages from a NuGet V2 feed into a NuGet repository that accepts push.
    /// </summary>
    public static class NugetFeedMirror
    {
        static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        const int PushTimeoutSeconds = 6 * 60 * 60;

        public static List<NugetV2Package> Select(IEnumerable<NugetV2Package> packages, IEnumerable<string> tags, bool latestOnly)
        {
            var matched = (packages ?? Enumerable.Empty<NugetV2Package>())
                .Where(package => package != null && NugetV2FeedClient.TagsMatch(package.Tags, tags))
                .ToList();

            if (!latestOnly)
                return matched;

            return matched
                .GroupBy(package => package.Id ?? "", StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(package => package.Version, Comparer<string>.Create(NugetV2FeedClient.CompareVersions))
                    .First())
                .ToList();
        }

        public static bool IsPresent(IEnumerable<NugetV2Package> destination, NugetV2Package package)
        {
            if (package == null || destination == null)
                return false;

            return destination.Any(existing =>
                string.Equals(existing.Id, package.Id, StringComparison.OrdinalIgnoreCase)
                && NugetV2FeedClient.SameVersion(existing.Version, package.Version));
        }

        public static async Task<MirrorResult> RunAsync(
            BeckhoffServer source,
            string nexusUrl,
            string nexusUsername,
            string nexusPassword,
            IEnumerable<string> tags,
            bool latestOnly,
            bool dryRun,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(nexusUrl))
                throw new ArgumentException("Nexus URL is required.", nameof(nexusUrl));

            var result = new MirrorResult { DryRun = dryRun };
            _logger.Info("[mirror] source: {0}", source.UrlBase);
            _logger.Info("[mirror] nexus: {0}", nexusUrl);

            List<NugetV2Package> sourcePackages;
            List<NugetV2Package> destinationPackages;
            using (var sourceClient = CreateClient(source.Username, source.Password))
            using (var nexusClient = CreateClient(nexusUsername, nexusPassword))
            {
                var sourceFeed = new NugetV2FeedClient(sourceClient);
                var nexusFeed = new NugetV2FeedClient(nexusClient);
                sourcePackages = await sourceFeed.ListAsync(source.UrlBase, cancellationToken);
                destinationPackages = await nexusFeed.ListAsync(nexusUrl, cancellationToken);
            }

            var selected = Select(sourcePackages, tags, latestOnly);
            var pending = new List<NugetV2Package>();
            foreach (var package in selected)
            {
                if (IsPresent(destinationPackages, package))
                {
                    _logger.Info("[mirror] skip {0} {1}", package.Id, package.Version);
                    result.Skipped.Add(Item(package));
                }
                else
                {
                    pending.Add(package);
                }
            }

            _logger.Info("[mirror] {0} selected, {1} already in Nexus, {2} to push", selected.Count, result.Skipped.Count, pending.Count);

            if (dryRun || pending.Count == 0)
            {
                foreach (var package in pending)
                {
                    _logger.Info("[mirror] would push {0} {1}", package.Id, package.Version);
                    result.Pushed.Add(Item(package));
                }

                return result;
            }

            PackageUpdateResource update;
            using (var pushClient = CreateClient(source.Username, source.Password))
            {
                var repository = CreateRepository(nexusUrl, nexusUsername, nexusPassword);
                update = await repository.GetResourceAsync<PackageUpdateResource>(cancellationToken);
                var apiKey = NexusApiKey(nexusUsername, nexusPassword);

                foreach (var package in pending)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = TempPackagePath(package);
                    try
                    {
                        _logger.Info("[mirror] download {0} {1}", package.Id, package.Version);
                        await DownloadAsync(pushClient, package.ContentUrl, file, cancellationToken);

                        _logger.Info("[mirror] push {0} {1}", package.Id, package.Version);
                        await update.Push(
                            new[] { file },
                            symbolSource: null,
                            timeoutInSecond: PushTimeoutSeconds,
                            disableBuffering: true,
                            getApiKey: _ => apiKey,
                            getSymbolApiKey: _ => null,
                            noServiceEndpoint: false,
                            skipDuplicate: true,
                            symbolPackageUpdateResource: null,
                            allowInsecureConnections: true,
                            log: NuGet.Common.NullLogger.Instance);

                        result.Pushed.Add(Item(package));
                    }
                    catch (Exception ex) when (!CancelableTask.IsCancellation(ex))
                    {
                        _logger.Error("[mirror] failed {0} {1}: {2}", package.Id, package.Version, ex.Message);
                        _logger.Trace(ex);
                        result.Failed.Add(new MirrorItem
                        {
                            Id = package.Id,
                            Version = package.Version,
                            Error = ex.Message
                        });
                    }
                    finally
                    {
                        TryDelete(file);
                    }
                }
            }

            _logger.Info("[mirror] pushed {0}, skipped {1}, failed {2}", result.Pushed.Count, result.Skipped.Count, result.Failed.Count);
            return result;
        }

        static MirrorItem Item(NugetV2Package package)
        {
            return new MirrorItem { Id = package.Id, Version = package.Version };
        }

        static HttpClient CreateClient(string username, string password)
        {
            var client = new HttpClient { Timeout = HttpTimeout() };
            if (!string.IsNullOrEmpty(password))
                client.DefaultRequestHeaders.Authorization = NugetV2FeedClient.BasicAuthorization(username, password);
            return client;
        }

        static TimeSpan HttpTimeout()
        {
#if NETFRAMEWORK
            return TimeSpan.FromMilliseconds(int.MaxValue);
#else
            return Timeout.InfiniteTimeSpan;
#endif
        }

        static SourceRepository CreateRepository(string nexusUrl, string username, string password)
        {
            var packageSource = new PackageSource(nexusUrl);
            if (!string.IsNullOrEmpty(password))
                packageSource.Credentials = new PackageSourceCredential(nexusUrl, username ?? "", password, true, null);

            return Repository.Factory.GetCoreV3(packageSource);
        }

        static string NexusApiKey(string username, string password)
        {
            if (string.IsNullOrEmpty(password))
                return null;
            if (string.IsNullOrEmpty(username))
                return password;
            return username + ":" + password;
        }

        static async Task DownloadAsync(HttpClient http, string url, string file, CancellationToken cancellationToken)
        {
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException("Package download returned " + (int)response.StatusCode + " for " + url);

                using (var input = await response.Content.ReadAsStreamAsync())
                using (var output = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output);
                }
            }
        }

        static string TempPackagePath(NugetV2Package package)
        {
            var name = (package.Id ?? "package") + "." + (package.Version ?? "0") + ".nupkg";
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return Path.Combine(Path.GetTempPath(), "twinpack-mirror-" + Guid.NewGuid().ToString("N") + "-" + name);
        }

        static void TryDelete(string file)
        {
            try
            {
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                _logger.Warn("[mirror] failed to delete {0}", file);
                _logger.Trace(ex);
            }
        }
    }
}
