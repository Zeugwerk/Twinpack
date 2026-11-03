using AdysTech.CredentialManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Protocol;

namespace TwinpackTests
{
    [TestClass]
    public class NugetServerLoginTest
    {
        [TestMethod]
        public async Task ApiKeyIsSentAsBasicAuthWithABlankUsernameAsync()
        {
            var apiKey = "secret-api-key";
            var expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + apiKey));

            await UsingFeedAsync(async (feed, requests) =>
            {
                Exception catalogError = null;
                var server = new NugetServer("API key feed", feed);
                try
                {
                    await server.LoginAsync("", apiKey);
                    Assert.IsTrue(server.LoggedIn, "a password with a blank username is a successful API-key login");
                    try
                    {
                        await server.GetCatalogAsync(null);
                    }
                    catch (Exception ex)
                    {
                        catalogError = ex;
                    }
                }
                finally
                {
                    CredentialManager.RemoveCredentials(feed);
                }

                var seen = Snapshot(requests);
                Assert.IsTrue(seen.Exists(request => request.Path.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0),
                    "the NuGet client never called the feed: " + Describe(seen) + (catalogError == null ? "" : " " + catalogError.GetType().Name));
                foreach (var request in seen)
                    Assert.AreEqual(expected, request.Authorization, request.Path);
            });
        }

        [TestMethod]
        public async Task UsernameAndPasswordAreStillSentAsBasicAuthAsync()
        {
            var expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes("alice:s3cret"));

            await UsingFeedAsync(async (feed, requests) =>
            {
                Exception catalogError = null;
                var server = new NugetServer("User feed", feed);
                try
                {
                    await server.LoginAsync("alice", "s3cret");
                    Assert.IsTrue(server.LoggedIn);
                    try
                    {
                        await server.GetCatalogAsync(null);
                    }
                    catch (Exception ex)
                    {
                        catalogError = ex;
                    }
                }
                finally
                {
                    CredentialManager.RemoveCredentials(feed);
                }

                var seen = Snapshot(requests);
                Assert.IsTrue(seen.Exists(request => request.Path.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0),
                    "the NuGet client never called the feed: " + Describe(seen) + (catalogError == null ? "" : " " + catalogError.GetType().Name));
                foreach (var request in seen)
                    Assert.AreEqual(expected, request.Authorization, request.Path);
            });
        }

        private static async Task UsingFeedAsync(Func<string, List<CapturedRequest>, Task> act)
        {
            HttpListener listener = null;
            var port = 0;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                port = 20000 + new Random().Next(20000);
                var candidate = new HttpListener();
                candidate.Prefixes.Add($"http://127.0.0.1:{port}/");
                try
                {
                    candidate.Start();
                    listener = candidate;
                    break;
                }
                catch (HttpListenerException)
                {
                    candidate.Close();
                }
            }

            Assert.IsNotNull(listener, "could not bind a local feed");

            var feed = $"http://127.0.0.1:{port}/index.json";
            var index = "{\"version\":\"3.0.0\",\"resources\":[{\"@id\":\"http://127.0.0.1:" + port + "/search\",\"@type\":[\"SearchQueryService/3.0.0-beta\",\"SearchQueryService\"]}]}";
            var requests = new List<CapturedRequest>();
            var worker = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await listener.GetContextAsync();
                    }
                    catch
                    {
                        break;
                    }

                    var path = context.Request.RawUrl ?? context.Request.Url?.AbsolutePath ?? "";
                    lock (requests)
                    {
                        requests.Add(new CapturedRequest
                        {
                            Path = path,
                            Authorization = context.Request.Headers["Authorization"]
                        });
                    }

                    var body = path.IndexOf("search", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "{\"totalHits\":0,\"data\":[]}"
                        : index;
                    var bytes = Encoding.UTF8.GetBytes(body);
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = bytes.Length;
                    try
                    {
                        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        context.Response.Close();
                    }
                    catch
                    {
                    }
                }
            });

            try
            {
                var work = act(feed, requests);
                var finished = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(20)));
                if (finished != work)
                    Assert.Fail("the feed did not answer in time: " + Describe(Snapshot(requests)));
                await work;
            }
            finally
            {
                try { listener.Stop(); } catch { }
                try { listener.Close(); } catch { }
                await Task.WhenAny(worker, Task.Delay(2000));
            }
        }

        private static List<CapturedRequest> Snapshot(List<CapturedRequest> requests)
        {
            lock (requests)
                return new List<CapturedRequest>(requests);
        }

        private static string Describe(List<CapturedRequest> requests)
        {
            if (requests.Count == 0)
                return "(no requests)";

            var lines = new StringBuilder();
            foreach (var request in requests)
                lines.Append(request.Path).Append(" auth=").Append(request.Authorization ?? "<none>").Append("; ");
            return lines.ToString();
        }

        private sealed class CapturedRequest
        {
            public string Path;
            public string Authorization;
        }
    }
}
