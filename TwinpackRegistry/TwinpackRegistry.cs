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
            var client = new GitHubClient(_header);
            var repositories = Encoding.ASCII.GetString(await client.Repository.Content.GetRawContent(repositoryOwner, repositoryName, "repositories.txt")).Trim();

            foreach(var repoUrl in repositories.Split('\n'))
            {
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
                        var iconFileName = $@"{filePath}\{libraryInfo.Title}_{libraryInfo.Version}.png";
                        var libraryFileName = $@"{filePath}\{libraryInfo.Title}_{libraryInfo.Version}.library";
                        var licenseFileName = $@"{filePath}\{libraryInfo.Title}_{libraryInfo.Version}.license";

                        var plc = new ConfigPlcProject()
                        {
                            Name = libraryInfo.Title,
                            Version = libraryInfo.Version,
                            //References =
                            //Packages =
                            Description = libraryInfo.Description,
                            //IconFile =
                            DisplayName = libraryInfo.Title,
                            DistributorName = libraryInfo.Company,
                            ProjectUrl = repoUrl,
                            Authors = libraryInfo.Author,
                            License = "LICENSE",
                            LicenseFile = licenseFileName,
                            IconFile = iconFileName,
                            BinaryDownloadUrl = asset.BrowserDownloadUrl,
                            Type = ConfigPlcProject.PlcProjectType.Library.ToString()
                        };

                        // only upload if the package is not published on Twinpack yet
                        var packageVersion = await _twinpackServer.GetPackageVersionAsync(plc.DistributorName, plc.Name, plc.Version);
                        if (packageVersion?.PackageVersionId == null)
                        {
                            config.Projects.Add(new ConfigProject()
                            {
                                Name = libraryInfo.Title,
                                Plcs = new List<ConfigPlcProject>() { plc }
                            });

                            Directory.CreateDirectory(new FileInfo(libraryFileName).DirectoryName);
                            File.WriteAllBytes(iconFileName, IconUtils.GenerateIdenticon(libraryInfo.Title));
                            File.WriteAllText(licenseFileName, license);
                            File.WriteAllBytes(libraryFileName, library);
                        }
                        else
                        {
                            _logger.Info($"Skipping already published package '{plc.Name}' (distributor: {plc.DistributorName}, version: {plc.Version})'");
                        }
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
