using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinpack.Models;
using Twinpack.Protocol;
using Twinpack.Protocol.Api;

namespace Twinpack.Core
{
    /// <summary>
    /// Which library files an install should hand to one XAE Shell session.
    /// The cache folder keeps older downloads. <see cref="FromCache"/> installs all of them.
    /// <see cref="FromDownloaded"/> installs only the packages a download run just fetched.
    /// </summary>
    public static class LibraryInstallPlan
    {
        public static List<PackageItem> FromCache(string cacheRoot)
        {
            var items = new List<PackageItem>();
            if (string.IsNullOrEmpty(cacheRoot) || !Directory.Exists(cacheRoot))
                return items;

            var files = EnumerateLibraryFiles(cacheRoot)
                .GroupBy(file => PlcLibraryPackage.LibraryTitle(file), StringComparer.OrdinalIgnoreCase)
                .Select(PreferCompiled)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files)
            {
                var version = new PackageVersionGetResponse
                {
                    Name = PlcLibraryPackage.LibraryTitle(file),
                    LibraryFiles = new List<string> { file },
                    Compiled = file.IndexOf(".compiled-library", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0
                };

                var repository = RepositoryDirectory(file);
                if (repository != null)
                    version.RepositoryFiles = Directory.GetFiles(repository, "*", SearchOption.AllDirectories).ToList();

                items.Add(new PackageItem
                {
                    PackageVersion = version
                });
            }

            return items;
        }

        public static List<PackageItem> FromDownloaded(IEnumerable<PackageItem> downloaded, string cacheRoot)
        {
            return TwinpackService.SelectDownloadedForInstall(downloaded, cacheRoot);
        }

        static string PreferCompiled(IEnumerable<string> files)
        {
            return files
                .OrderBy(file => file.IndexOf(".compiled-library", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        static IEnumerable<string> EnumerateLibraryFiles(string root)
        {
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                string[] files;
                try { files = Directory.GetFiles(dir); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { continue; }

                foreach (var file in files)
                {
                    if (PlcLibraryPackage.IsLibraryFile(file))
                        yield return file;
                }

                string[] subdirs;
                try { subdirs = Directory.GetDirectories(dir); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { continue; }

                foreach (var sub in subdirs)
                {
                    var name = Path.GetFileName(sub);
                    if (name != null && name.EndsWith(".repository", StringComparison.OrdinalIgnoreCase))
                        continue;
                    stack.Push(sub);
                }
            }
        }

        static string RepositoryDirectory(string libraryFile)
        {
            var directory = Path.GetDirectoryName(libraryFile);
            if (string.IsNullOrEmpty(directory))
                return null;

            var stem = PlcLibraryPackage.LibraryTitle(libraryFile);
            var candidate = Path.Combine(directory, stem + ".repository");
            return Directory.Exists(candidate) ? candidate : null;
        }
    }
}
