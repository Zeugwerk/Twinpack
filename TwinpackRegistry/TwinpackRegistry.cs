using Microsoft.Win32;
using NLog;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Lifetime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Twinpack.Configuration;
using Twinpack.Core;
using Twinpack.Models;
using Twinpack.Protocol;
using Twinpack.Protocol.Api;

namespace Twinpack
{
    public class TwinpackRegistry
    {
        readonly Logger _logger = LogManager.GetCurrentClassLogger();
        ProductHeaderValue _header = new ProductHeaderValue("Twinpack-Registry");
        List<IPackageServer> _packageServers;
        List<string> _licenseFileHeuristics = new List<string>() { "LICENSE", "LICENSE.txt", "LICENSE.md" };

        public TwinpackRegistry(List<IPackageServer> packageServers)
        {
            _packageServers = packageServers;
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

        public async Task UpdateDownloadsAsync(string repositoryOwner, string repositoryName, string token = null, bool dryRun = true)
        {
            var client = new GitHubClient(_header);
            if (token != null)
                client.Credentials = new Credentials(token);

            var repositories = Encoding.ASCII.GetString(await client.Repository.Content.GetRawContent(repositoryOwner, repositoryName, "repositories.txt")).Trim();

            foreach (var repoUrl in repositories.Split('\n'))
            {
                var (owner, repo) = ParseGitHubRepoUrl(repoUrl);
                var releases = await client.Repository.Release.GetAll(owner, repo);

                foreach(var release in releases)
                {
                    var assets = release.Assets.Where(x => x.Name.EndsWith(".library"));
                    if (!assets.Any())
                        continue;

                    foreach (var asset in assets)
                    {
                        try
                        {
                            var downloads = asset.DownloadCount;
                            var library = await DownloadAsync(asset.BrowserDownloadUrl);
                            var libraryInfo = LibraryReader.Read(library, dumpFilenamePrefix: $"{repositoryOwner}_{repositoryName}_{asset.Name}.library");

                            // only upload if the package is not published on Twinpack yet
                            foreach(var packageServer in _packageServers.Where(x => x.Connected))
                            {
                                var packageVersion = await packageServer.GetPackageVersionAsync(new PlcLibrary { DistributorName = libraryInfo.Company, Name = libraryInfo.Title, Version = libraryInfo.Version }, null, null, null);
                                if (packageVersion?.Name != null)
                                {
                                    _logger.Info($"Updating counter of '{libraryInfo.Title}' (distributor: {libraryInfo.Company}, version: {libraryInfo.Version})' to {downloads}");

                                    if (!dryRun)
                                        await (packageServer as TwinpackServer)?.PutPackageVersionDownloadsAsync(
                                            new PackageVersionDownloadsPutRequest { PackageVersionId = packageVersion.PackageVersionId, Downloads = downloads });

                                    break;
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.Trace(ex);
                            _logger.Warn(ex.Message);
                        }
                    }
                }
            }
        }

        public async Task DownloadAsync(string repositoryOwner, string repositoryName, string token=null)
        {
            var config = new Config()
            {
                WorkingDirectory = ".",
                FilePath = ".Zeugwerk/config.json"
            };

            _logger.Info("Setup GitHub client");

            var client = new GitHubClient(_header);
            if (token != null)
            {
                _logger.Info("Using API token");
                client.Credentials = new Credentials(token);
            }
            else
            {
                _logger.Info("No API token present");
            }

            var repositories = Encoding.ASCII.GetString(await client.Repository.Content.GetRawContent(repositoryOwner, repositoryName, "repositories.txt")).Trim();

            foreach(var repoUrl in repositories.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(repoUrl))
                    continue;

                var (owner, repo) = ParseGitHubRepoUrl(repoUrl);
                _logger.Info(new string('-', 3) + $" {owner}:{repo}");

                var releases = await client.Repository.Release.GetAll(owner, repo);

                _logger.Info($"Found {releases.Where(x => !x.Prerelease).Count()} offical releases and {releases.Where(x => x.Prerelease).Count()} pre-releases");

                var latestRelease = releases.Where(x => !x.Prerelease)
                                            .OrderByDescending(x => x.PublishedAt)
                                            .FirstOrDefault();

                if(latestRelease != null)
                {
                    await DownloadReleaseAsync(config, client, repoUrl, latestRelease, owner, repo);
                }
                else
                {
                    _logger.Warn($"Skipping '{repoUrl}', it has no offical release");
                }
            }

            ConfigFactory.Save(config);
        }

        public async Task DownloadReleaseAsync(Config config, GitHubClient client, string repoUrl, Release release, string owner, string repo)
        {
            var target = "TC3.1";

            var assets = release.Assets.Where(x => x.Name.EndsWith(".library"));
            if (!assets.Any())
            {
                _logger.Warn($"Release '{release.Name} ({release.TagName})' doesn't contain any .library files");
                return;
            }

            _logger.Info($"Downloading .library files of '{release.TagName}'");
            foreach (var asset in assets)
            {
                _logger.Info($"Processing {asset.Name}");

                try
                {
                    var license = await RetrieveLicenseAsync(client, owner, repo);
                    var library = await DownloadAsync(asset.BrowserDownloadUrl);
                    var libraryInfo = LibraryReader.Read(library, dumpFilenamePrefix: $"{owner}_{repo}_{asset.Name}.library");
                    var filePath = $@"{Protocol.TwinpackServer.DefaultLibraryCachePath}\{target}";
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
                    var twinpackServer = _packageServers.Where(x => x is TwinpackServer).First();
                    var packageVersion = await twinpackServer.GetPackageVersionAsync(new PlcLibrary { DistributorName = plc.DistributorName, Name = plc.Name, Version = plc.Version }, null, null, null);
                    if (packageVersion?.Name == null)
                    {
                        _logger.Info($"This release '{release.Name} ({release.TagName})' is not yet published to Twinpack");

                        foreach (var dependency in libraryInfo.Dependencies.Where(x => x.Version == "*" || Version.TryParse(x.Version, out _) == true))
                        {
                            dependency.Version = dependency.Version == "*" ? null : dependency.Version;
                            PackageVersionGetResponse resolvedDependency = null;
                            foreach (var depPackageServer in _packageServers.Where(x => x.Connected))
                            {
                                if (resolvedDependency != null)
                                    break;

                                resolvedDependency = await depPackageServer.ResolvePackageVersionAsync(new PlcLibrary { DistributorName = dependency.DistributorName, Name = dependency.Name, Version = dependency.Version }, null, null, null);
                                if (resolvedDependency.Name != null && (resolvedDependency.Version == dependency.Version || dependency.Version == null))
                                {
                                    _logger.Info($"Dependency '{dependency.Name}' (distributor: {dependency.DistributorName}, version: {dependency.Version}) located on {depPackageServer.UrlBase}");
                                    plc.Packages = plc.Packages.Append(
                                        new ConfigPlcPackage()
                                        {
                                            Name = resolvedDependency.Name,
                                            DistributorName = resolvedDependency.DistributorName,
                                            Version = resolvedDependency.Version,
                                            Configuration = resolvedDependency.Configuration,
                                            Branch = resolvedDependency.Branch,
                                            Target = resolvedDependency.Target
                                        }).ToList();
                                }
                            }

                            if(resolvedDependency == null)
                            {
                                _logger.Info($"Dependency '{dependency.Name}' (distributor: {dependency.DistributorName}, version: {dependency.Version})");
                                plc.Packages = plc.Packages.Append(
                                    new ConfigPlcPackage()
                                    {
                                        Name = dependency.Name,
                                        DistributorName = dependency.DistributorName,
                                        Version = dependency.Version
                                    }).ToList();
                            }
                        }

                        config.Projects.Add(new ConfigProject()
                        {
                            Name = libraryInfo.Title,
                            Plcs = new List<ConfigPlcProject>() { plc }
                        });

                        Directory.CreateDirectory(new FileInfo(libraryFileName).DirectoryName);
                        File.WriteAllBytes(iconFileName, IconUtils.GenerateIdenticon(libraryInfo.Title));
                        File.WriteAllText(licenseFileName, license);
                        File.WriteAllBytes(libraryFileName, library);
                        break;
                    }
                    else
                    {
                        _logger.Info($"Skipping {plc.Name} {plc.Version} (distributor: {plc.DistributorName})'");
                    }

                }
                catch (Exception ex)
                {
                    _logger.Trace(ex);
                    _logger.Warn(ex.Message);
                }
            }
        }
    }
}
