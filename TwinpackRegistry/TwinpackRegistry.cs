using Microsoft.Win32;
using NLog;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Models;

namespace Twinpack
{
    public class TwinpackRegistry
    {
        readonly Logger _logger = LogManager.GetCurrentClassLogger();
        ProductHeaderValue _header = new ProductHeaderValue("Twinpack-Registry");
        TwinpackServer _twinpackServer;
        List<string> _licenseFileHeuristics = new List<string>() { "LICENSE", "LICENSE.txt", "LICENSE.md" };

        public TwinpackRegistry(TwinpackServer twinpackServer)
        {
            _twinpackServer = twinpackServer;
        }

        static (string Owner, string RepoName) ParseGitHubRepoUrl(string url)
        {
            // Check if the URL starts with the GitHub repository prefix
            if (url.StartsWith("https://github.com/"))
            {
                // Split the URL into parts
                var parts = url.Substring("https://github.com/".Length).Split('/');

                // Ensure there are at least two parts (owner and repository name)
                if (parts.Length >= 2)
                {
                    var owner = parts[0];
                    var repoName = parts[1];

                    return (owner, repoName);
                }
            }

            return (null, null);
        }

        public async Task<string> RetrieveLicenseAsync(GitHubClient client, string repositoryOwner, string repositoryName)
        {
            string license = null;

            foreach (var licenseFile in _licenseFileHeuristics)
            {
                try
                {
                    license = Encoding.ASCII.GetString(await client.Repository.Content.GetRawContent(repositoryOwner, repositoryName, licenseFile));
                    return license;
                }
                catch
                {
                }
            }

            return license;
        }

        async Task<byte[]> DownloadAsync(string url)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public async Task DownloadAsync(string repositoryOwner, string repositoryName, string cachePath = null)
        {
            var config = new Config()
            {
                WorkingDirectory = ".",
                FilePath = ".Zeugwerk/config.json"
            };

            var target = "TC3.1";
            var suffix = "library";
            var registry = new GitHubClient(_header);
            var repositories = Encoding.ASCII.GetString(await registry.Repository.Content.GetRawContent(repositoryOwner, repositoryName, "repositories.txt")).Trim();

            // todo: foreach async
            foreach(var repoUrl in repositories.Split('\n'))
            {
                var client = new GitHubClient(_header);
                var (owner, repo) = ParseGitHubRepoUrl(repoUrl);
                var latestRelease = await client.Repository.Release.GetLatest(owner, repo);
                
                foreach(var asset in latestRelease.Assets.Where(x => x.Name.EndsWith(".library")))
                {
                    try
                    {
                        var license = await RetrieveLicenseAsync(client, owner, repo);
                        var library = await DownloadAsync(asset.BrowserDownloadUrl);
                        var libraryInfo = LibraryPropertyReader.Read(library);
                        var filePath = $@"{TwinpackServer.DefaultLibraryCachePath}\{target}";
                        var libraryFileName = $@"{filePath}\{libraryInfo.Name}_{libraryInfo.Version}.{suffix}";
                        var licenseFileName = $@"{filePath}\LICENSE_{libraryInfo.Name}_{libraryInfo.Version}.{suffix}";

                        config.Projects.Add(new ConfigProject()
                        {
                            Name = libraryInfo.Name,
                            Plcs = new List<ConfigPlcProject> ()
                            {
                                new ConfigPlcProject()
                                {
                                    Name = libraryInfo.Name,
                                    Version = libraryInfo.Version,
                                    //References =
                                    //Packages =
                                    Description = libraryInfo.Description,
                                    //IconFile =
                                    DisplayName = libraryInfo.Name,
                                    DistributorName = libraryInfo.Company,
                                    ProjectUrl = repoUrl,
                                    Authors = libraryInfo.Author,
                                    License = "LICENSE",
                                    LicenseFile = licenseFileName,
                                    Type = ConfigPlcProject.PlcProjectType.Library.ToString()
                                }
                            }
                        });

                        Directory.CreateDirectory(new FileInfo(libraryFileName).DirectoryName);
                        if(File.Exists(libraryFileName))
                            throw new Exception($"{libraryFileName} already exists");

                        File.WriteAllText(licenseFileName, license);
                        File.WriteAllBytes(libraryFileName, library);
                    }
                    catch (Exception ex)
                    {
                        _logger.Trace(ex);
                        _logger.Warn(ex.Message);
                    }
                }
            }

            ConfigFactory.Save(config);
        }
    }
}
