#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Versioning;

namespace Twinpack.Protocol
{
    public sealed class NugetV2Package
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Tags { get; set; }
        public string ContentUrl { get; set; }
    }

    public sealed class NugetV2Page
    {
        public List<NugetV2Package> Packages { get; } = new List<NugetV2Package>();
        public string NextUrl { get; set; }
    }

    /// <summary>
    /// Reads a NuGet V2 OData feed. Each version is one <c>Packages()</c> entry.
    /// </summary>
    public class NugetV2FeedClient
    {
        public const int PageSize = 100;

        static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
        static readonly XNamespace Data = "http://schemas.microsoft.com/ado/2007/08/dataservices";

        readonly HttpClient _http;

        public NugetV2FeedClient(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public static string FirstPageUrl(string feedUrl)
        {
            if (string.IsNullOrWhiteSpace(feedUrl))
                throw new ArgumentException("Feed URL is required.", nameof(feedUrl));

            return feedUrl.TrimEnd('/') + "/Packages()?$top=" + PageSize;
        }

        public static string DownloadUrl(string feedUrl, string id, string version)
        {
            var root = feedUrl.EndsWith("/", StringComparison.Ordinal) ? feedUrl : feedUrl + "/";
            return root + "package/" + Uri.EscapeDataString(id) + "/" + Uri.EscapeDataString(version);
        }

        public async Task<List<NugetV2Package>> ListAsync(string feedUrl, CancellationToken cancellationToken = default)
        {
            var page = FirstPageUrl(feedUrl);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packages = new List<NugetV2Package>();

            while (!string.IsNullOrEmpty(page))
            {
                if (!seen.Add(page))
                    break;

                var requestUri = new Uri(page);
                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                {
                    request.Headers.Accept.ParseAdd("application/atom+xml");
                    request.Headers.Accept.ParseAdd("application/xml");
                    request.Headers.TryAddWithoutValidation("DataServiceVersion", "2.0");
                    request.Headers.TryAddWithoutValidation("MaxDataServiceVersion", "2.0");

                    using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new InvalidOperationException("Package feed returned " + (int)response.StatusCode + " for " + requestUri);

                        var xml = await response.Content.ReadAsStringAsync();
                        var parsed = Parse(xml, requestUri, feedUrl);
                        packages.AddRange(parsed.Packages);
                        page = parsed.NextUrl;
                    }
                }
            }

            return packages;
        }

        public static NugetV2Page Parse(string xml, Uri requestUri, string feedUrl)
        {
            var document = XDocument.Parse(xml);
            var feed = document.Root;
            if (feed == null)
                throw new InvalidOperationException("Package feed returned an empty document.");

            var page = new NugetV2Page();
            var xmlBase = (string)feed.Attribute(XNamespace.Xml + "base");
            var contentBase = string.IsNullOrEmpty(xmlBase) ? requestUri : new Uri(requestUri, xmlBase);

            var next = feed.Elements(Atom + "link")
                .FirstOrDefault(link => string.Equals((string)link.Attribute("rel"), "next", StringComparison.OrdinalIgnoreCase));
            var nextHref = (string)next?.Attribute("href");
            if (!string.IsNullOrWhiteSpace(nextHref))
                page.NextUrl = new Uri(requestUri, nextHref).AbsoluteUri;

            foreach (var entry in feed.Elements(Atom + "entry"))
            {
                var id = Text(entry, "Id");
                var version = Text(entry, "Version");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
                    continue;

                var src = (string)entry.Element(Atom + "content")?.Attribute("src");
                string contentUrl;
                if (string.IsNullOrWhiteSpace(src))
                    contentUrl = DownloadUrl(feedUrl, id.Trim(), version.Trim());
                else
                    contentUrl = new Uri(contentBase, src).AbsoluteUri;

                page.Packages.Add(new NugetV2Package
                {
                    Id = id.Trim(),
                    Version = version.Trim(),
                    Tags = Text(entry, "Tags") ?? "",
                    ContentUrl = contentUrl
                });
            }

            return page;
        }

        /// <summary>
        /// A tag matches when it is a whole whitespace-separated token, compared case-insensitively.
        /// </summary>
        public static bool TagsMatch(string tags, IEnumerable<string> wanted)
        {
            if (wanted == null)
                return false;

            var tokens = new HashSet<string>(
                (tags ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

            return wanted.Any(tag => !string.IsNullOrWhiteSpace(tag) && tokens.Contains(tag.Trim()));
        }

        public static bool SameVersion(string left, string right)
        {
            if (NuGetVersion.TryParse(left, out var parsedLeft) && NuGetVersion.TryParse(right, out var parsedRight))
                return VersionComparer.Default.Equals(parsedLeft, parsedRight);

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public static int CompareVersions(string left, string right)
        {
            var leftOk = NuGetVersion.TryParse(left, out var parsedLeft);
            var rightOk = NuGetVersion.TryParse(right, out var parsedRight);
            if (leftOk && rightOk)
                return VersionComparer.Default.Compare(parsedLeft, parsedRight);
            if (leftOk)
                return 1;
            if (rightOk)
                return -1;
            return StringComparer.OrdinalIgnoreCase.Compare(left, right);
        }

        public static AuthenticationHeaderValue BasicAuthorization(string username, string password)
        {
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes((username ?? "") + ":" + password));
            return new AuthenticationHeaderValue("Basic", token);
        }

        static string Text(XElement entry, string localName)
        {
            return entry.Descendants(Data + localName).FirstOrDefault()?.Value;
        }
    }
}
