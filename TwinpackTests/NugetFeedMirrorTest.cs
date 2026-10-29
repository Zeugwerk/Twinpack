using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class NugetFeedMirrorTest
    {
        const string Feed = "https://feed.example/nuget/";

        [TestMethod]
        public void Parse_ReadsEntriesAndFollowsRelativeNextLink()
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed xml:base=""https://feed.example/nuget/"" xmlns=""http://www.w3.org/2005/Atom"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  <link rel=""self"" href=""Packages()"" />
  <link rel=""next"" href=""Packages()?$skip=100"" />
  <entry>
    <content type=""application/zip"" src=""package/TwinCAT.XAE.PLC.Lib.Tc2_Standard/3.4.7.0"" />
    <m:properties>
      <d:Id>TwinCAT.XAE.PLC.Lib.Tc2_Standard</d:Id>
      <d:Version>3.4.7.0</d:Version>
      <d:Tags xml:space=""preserve""> Library Tc2_Standard </d:Tags>
    </m:properties>
  </entry>
  <entry>
    <m:properties>
      <d:Id>TwinCAT.XAE.PLC.Lib.Tc2_Math</d:Id>
      <d:Version>1.0.0</d:Version>
      <d:Tags>library</d:Tags>
    </m:properties>
  </entry>
</feed>";

            var page = NugetV2FeedClient.Parse(xml, new Uri(NugetV2FeedClient.FirstPageUrl(Feed)), Feed);

            Assert.AreEqual("https://feed.example/nuget/Packages()?$skip=100", page.NextUrl);
            Assert.AreEqual(2, page.Packages.Count);
            Assert.AreEqual("TwinCAT.XAE.PLC.Lib.Tc2_Standard", page.Packages[0].Id);
            Assert.AreEqual("3.4.7.0", page.Packages[0].Version);
            Assert.AreEqual("https://feed.example/nuget/package/TwinCAT.XAE.PLC.Lib.Tc2_Standard/3.4.7.0", page.Packages[0].ContentUrl);
            Assert.AreEqual("https://feed.example/nuget/package/TwinCAT.XAE.PLC.Lib.Tc2_Math/1.0.0", page.Packages[1].ContentUrl);
        }

        [TestMethod]
        public async Task ListAsync_FollowsNextLinkUntilTheLastPageAsync()
        {
            var first = NugetV2FeedClient.FirstPageUrl(Feed);
            var second = "https://feed.example/nuget/Packages()?$skip=1";
            var handler = new ScriptedHandler(request =>
            {
                var url = request.RequestUri.AbsoluteUri;
                string xml;
                if (string.Equals(url, first, StringComparison.OrdinalIgnoreCase))
                {
                    xml = Page("Pkg.One", "1.0.0", "library", second);
                }
                else if (string.Equals(url, second, StringComparison.OrdinalIgnoreCase))
                {
                    xml = Page("Pkg.Two", "2.0.0", "library", null);
                }
                else
                {
                    throw new InvalidOperationException(url);
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(xml, Encoding.UTF8, "application/atom+xml")
                };
                return response;
            });

            List<NugetV2Package> packages;
            using (var http = new HttpClient(handler))
            {
                packages = await new NugetV2FeedClient(http).ListAsync(Feed);
            }

            CollectionAssert.AreEqual(
                new[] { first, second },
                handler.Requests.Select(uri => uri.AbsoluteUri).ToArray());
            Assert.AreEqual("Pkg.One", packages[0].Id);
            Assert.AreEqual("Pkg.Two", packages[1].Id);
            Assert.IsTrue(handler.Requests.All(uri =>
                handler.Accepts[uri.AbsoluteUri].IndexOf("application/atom+xml", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [TestMethod]
        public void TagsMatch_IsWholeWordAndCaseInsensitive()
        {
            Assert.IsTrue(NugetV2FeedClient.TagsMatch(" Library product ", new[] { "library" }));
            Assert.IsTrue(NugetV2FeedClient.TagsMatch("stable product", new[] { "Library", "product" }));
            Assert.IsFalse(NugetV2FeedClient.TagsMatch("library", new[] { "lib" }));
            Assert.IsFalse(NugetV2FeedClient.TagsMatch("MotionControl", new[] { "Motion" }));
            Assert.IsFalse(NugetV2FeedClient.TagsMatch("library", new[] { "  " }));
        }

        [TestMethod]
        public void Select_LatestOnlyKeepsTheHighestVersionPerPackage()
        {
            var packages = new[]
            {
                Package("A", "1.0.0-beta", "library"),
                Package("A", "1.0.0", "library"),
                Package("A", "0.9.0", "other"),
                Package("B", "2.0.0-preview", "library"),
                Package("C", "1.0.0", "product")
            };

            var latest = NugetFeedMirror.Select(packages, new[] { "library" }, latestOnly: true);
            Assert.AreEqual(2, latest.Count);
            Assert.AreEqual("1.0.0", latest.Single(package => package.Id == "A").Version);
            Assert.AreEqual("2.0.0-preview", latest.Single(package => package.Id == "B").Version);

            var all = NugetFeedMirror.Select(packages, new[] { "library" }, latestOnly: false);
            Assert.AreEqual(3, all.Count);
        }

        [TestMethod]
        public void IsPresent_TreatsNormalizedNuGetVersionsAsTheSame()
        {
            var destination = new[] { Package("A", "1.0.0", "library") };

            Assert.IsTrue(NugetFeedMirror.IsPresent(destination, Package("a", "1.0.0.0", "library")));
            Assert.IsFalse(NugetFeedMirror.IsPresent(destination, Package("A", "1.0.1", "library")));
            Assert.IsTrue(NugetV2FeedClient.SameVersion("1.0.0", "1.0.0.0"));
        }

        static NugetV2Package Package(string id, string version, string tags)
        {
            return new NugetV2Package { Id = id, Version = version, Tags = tags };
        }

        static string Page(string id, string version, string tags, string next)
        {
            var nextLink = next == null ? "" : @"<link rel=""next"" href=""" + next + @""" />";
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<feed xmlns=""http://www.w3.org/2005/Atom"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
  " + nextLink + @"
  <entry>
    <content type=""application/zip"" src=""https://feed.example/nuget/package/" + id + "/" + version + @""" />
    <m:properties>
      <d:Id>" + id + @"</d:Id>
      <d:Version>" + version + @"</d:Version>
      <d:Tags>" + tags + @"</d:Tags>
    </m:properties>
  </entry>
</feed>";
        }

        sealed class ScriptedHandler : HttpMessageHandler
        {
            readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            public List<Uri> Requests { get; } = new List<Uri>();
            public Dictionary<string, string> Accepts { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri);
                Accepts[request.RequestUri.AbsoluteUri] = request.Headers.Accept.ToString();
                return Task.FromResult(_respond(request));
            }
        }
    }
}
