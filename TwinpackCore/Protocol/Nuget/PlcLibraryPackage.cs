using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Twinpack.Protocol
{
    /// <summary>
    /// A PLC library file taken out of a NuGet package or the MSI it contains.
    /// Beckhoff packages describe the library TwinCAT actually installs in a sibling
    /// <c>browsercache</c> (<c>Library Name="Title, Version (Company)"</c>) and, failing that,
    /// in <c>projectinfo</c> (<c>TcLibraryId</c> = <c>Company|Title|Version</c>).
    /// </summary>
    public sealed class ExtractedPlcLibrary
    {
        public string Path { get; set; } = "";
        public string Title { get; set; } = "";
        public string Version { get; set; } = "";
        public string Distributor { get; set; } = "";
        public bool Compiled { get; set; }

        public bool IdentityKnown { get; set; }
    }

    public static class PlcLibraryPackage
    {
        static readonly Regex LibraryNameAttribute = new Regex(
            @"^(.+),\s*(\d+(?:\.\d+){1,3})\s*\((.+)\)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        static readonly Regex TcLibraryId = new Regex(
            @"TcLibraryId\s*([^|]{1,200})\|([^|]{1,200})\|(\d+\.\d+\.\d+(?:\.\d+)?)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool IsLibraryFile(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".library", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".compiled-library", StringComparison.OrdinalIgnoreCase);
        }

        public static string LibraryTitle(string path)
        {
            var name = Path.GetFileName(path);
            const string compiled = ".compiled-library";
            if (name.EndsWith(compiled, StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - compiled.Length);

            return Path.GetFileNameWithoutExtension(name);
        }

        public static ExtractedPlcLibrary Describe(string libraryPath)
        {
            var library = new ExtractedPlcLibrary
            {
                Path = libraryPath,
                Title = LibraryTitle(libraryPath),
                Compiled = libraryPath.EndsWith(".compiled-library", StringComparison.OrdinalIgnoreCase)
            };

            var directory = Path.GetDirectoryName(libraryPath);
            if (!string.IsNullOrEmpty(directory))
            {
                if (!TryReadBrowserCache(Path.Combine(directory, "browsercache"), library))
                    TryReadProjectInfo(Path.Combine(directory, "projectinfo"), library);

                ApplyRepositoryLayout(directory, library);
            }

            return library;
        }

        public static IReadOnlyList<ExtractedPlcLibrary> DescribeAll(IEnumerable<string> libraryPaths)
        {
            return libraryPaths.Where(IsLibraryFile).Select(Describe).ToList();
        }

        /// <summary>
        /// The library this package is for. Every library in the package is installed; only this one
        /// is added as a reference. The package id's last segment is the Beckhoff library name
        /// (<c>TwinCAT.XAE.PLC.Lib.Motion.MC3.Tc3_Mc3Base</c> is <c>Tc3_Mc3Base</c>, not the
        /// <c>MC3</c> product family in the tags).
        /// </summary>
        public static ExtractedPlcLibrary ChooseReference(IReadOnlyList<ExtractedPlcLibrary> libraries, string packageId, string packageTitle)
        {
            if (libraries == null || libraries.Count == 0)
                return null;

            if (libraries.Count == 1)
                return libraries[0];

            var idSuffix = packageId?.Split('.').LastOrDefault();
            var byId = libraries.FirstOrDefault(x => string.Equals(x.Title, idSuffix, StringComparison.OrdinalIgnoreCase));
            if (byId != null)
                return byId;

            return libraries.FirstOrDefault(x => string.Equals(x.Title, packageTitle, StringComparison.OrdinalIgnoreCase));
        }

        static bool TryReadBrowserCache(string path, ExtractedPlcLibrary library)
        {
            if (!File.Exists(path))
                return false;

            string name;
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var document = XDocument.Load(stream);
                    name = document.Root?.Attribute("Name")?.Value;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return ApplyIdentity(name, library);
        }

        static bool TryReadProjectInfo(string path, ExtractedPlcLibrary library)
        {
            if (!File.Exists(path))
                return false;

            var text = Encoding.UTF8.GetString(File.ReadAllBytes(path)).Replace("\0", "");
            var match = TcLibraryId.Match(text);
            if (!match.Success)
                return false;

            library.Distributor = match.Groups[1].Value.Trim();
            library.Title = match.Groups[2].Value.Trim();
            library.Version = match.Groups[3].Value.Trim();
            library.IdentityKnown = true;
            return true;
        }

        static void ApplyRepositoryLayout(string directory, ExtractedPlcLibrary library)
        {
            var versionFolder = Path.GetFileName(directory);
            var titleFolder = Path.GetFileName(Path.GetDirectoryName(directory));
            var companyFolder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(directory)));
            if (!string.Equals(titleFolder, library.Title, StringComparison.OrdinalIgnoreCase))
                return;

            if (string.IsNullOrEmpty(library.Version))
                library.Version = versionFolder;
            if (string.IsNullOrEmpty(library.Distributor))
                library.Distributor = companyFolder;
        }

        static bool ApplyIdentity(string libraryName, ExtractedPlcLibrary library)
        {
            if (string.IsNullOrWhiteSpace(libraryName))
                return false;

            var match = LibraryNameAttribute.Match(libraryName.Trim());
            if (!match.Success)
                return false;

            library.Title = match.Groups[1].Value.Trim();
            library.Version = match.Groups[2].Value.Trim();
            library.Distributor = match.Groups[3].Value.Trim();
            library.IdentityKnown = true;
            return true;
        }
    }
}
