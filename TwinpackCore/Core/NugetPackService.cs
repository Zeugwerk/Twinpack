using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinpack.Configuration;
using Twinpack.Protocol;

namespace Twinpack.Core
{
    /// <summary>
    /// Builds NuGet packages for TwinCAT PLCs. This is what `twinpack nuget pack` and `zkmake nuget
    /// pack` both call, so a library package is identical no matter which tool produced it.
    /// <para>
    /// The field mapping is not free choice, it is dictated by how <see cref="NugetServer"/> reads a
    /// package back: the package <c>id</c> becomes the Twinpack package name, <c>title</c> becomes
    /// the title matched against the TwinCAT library title, and <c>authors</c> becomes the
    /// distributor name. Putting the library's Author into <c>authors</c> instead of its Company
    /// would publish the package under the wrong distributor.
    /// </para>
    /// </summary>
    public static class NugetPackService
    {
        /// <summary>Tag that marks a package as containing a TwinCAT library. Always required.</summary>
        public const string LibraryTag = "library";

        /// <summary>Tag <see cref="NugetServer"/> looks for to tell a compiled library apart.</summary>
        public const string CompiledLibraryTag = "tp-compiled-library";

        public const string ApplicationTag = "application";

        /// <summary>
        /// Twinpack tags start with this. One without <c>=</c> is a flag (<c>tp-compiled-library</c>),
        /// one with <c>=</c> carries a value (<c>tp-library-version=1.2.3.0</c>).
        /// </summary>
        public const string TwinpackTagPrefix = "tp-";

        /// <summary>
        /// Key of the tag that records the version TwinCAT built the library under, e.g.
        /// <c>tp-library-version=1.2.3.0</c>. NuGet drops a revision of 0 from the package version,
        /// so <c>1.2.3</c> on a feed may be a library versioned <c>1.2.3</c> or <c>1.2.3.0</c>, and a
        /// reference with the wrong one does not resolve in TwinCAT. The value never carries a
        /// prerelease label, that stays in the package version.
        /// </summary>
        public const string LibraryVersionTagKey = "tp-library-version";

        /// <summary>The library version tag for a PLC versioned <paramref name="version"/>, or null if it is no TwinCAT library version.</summary>
        public static string LibraryVersionTag(string version)
        {
            if (string.IsNullOrWhiteSpace(version) || version.Trim() == "*")
                return null;

            string libraryVersion;
            try
            {
                libraryVersion = AutomationInterface.TwinCATProjectVersion(version);
            }
            catch (ArgumentException)
            {
                return null;
            }

            return IsLibraryVersion(libraryVersion) ? $"{LibraryVersionTagKey}={libraryVersion}" : null;
        }

        /// <summary>The version recorded by <see cref="LibraryVersionTag"/>, or null if <paramref name="tags"/> carry none.</summary>
        public static string LibraryVersionFromTags(string tags)
        {
            return TwinpackTagValues(tags).TryGetValue(LibraryVersionTagKey, out var version) && IsLibraryVersion(version)
                ? version
                : null;
        }

        /// <summary>
        /// The <c>key=value</c> Twinpack tags in a package's tag list. Feeds hand tags back separated
        /// by spaces or, in some web views, by commas.
        /// </summary>
        public static IReadOnlyDictionary<string, string> TwinpackTagValues(string tags)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in (tags ?? "").Split(new[] { ' ', ',', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = tag.IndexOf('=');
                if (!tag.StartsWith(TwinpackTagPrefix, StringComparison.OrdinalIgnoreCase)
                    || separator <= TwinpackTagPrefix.Length
                    || separator == tag.Length - 1)
                    continue;

                values[tag.Substring(0, separator)] = tag.Substring(separator + 1);
            }

            return values;
        }

        static bool IsLibraryVersion(string version)
        {
            return !string.IsNullOrEmpty(version) && Version.TryParse(version, out _);
        }

        /// <summary>Where both CLIs put packages when no output directory is given.</summary>
        public static string DefaultOutputPath => Path.Combine(Directory.GetCurrentDirectory(), ".Zeugwerk", "nupkg");

        /// <summary>
        /// Packs a single built library. <paramref name="libraryFilePath"/> is the
        /// <c>.library</c>/<c>.compiled-library</c> produced by the build, <paramref name="target"/>
        /// the TwinCAT target it was built for (e.g. <c>TC3.1</c>), which is also the folder the
        /// library ends up in inside the package.
        /// <paramref name="metadata"/> is the fully resolved publish metadata, i.e. derived values
        /// with any caller overrides already applied. Pass null to derive it here from the library
        /// binary, which is the same baseline <c>twinpack push</c> uses.
        /// </summary>
        public static string PackLibrary(
            ConfigPlcProject plc,
            string libraryFilePath,
            string target,
            bool compiled,
            string outputDirectory,
            PlcPublishMetadata metadata = null,
            IEnumerable<NugetPackFile> additionalFiles = null)
        {
            return PackLibrary(plc, libraryFilePath, target, compiled, outputDirectory, metadata, additionalFiles, libraryVersionKnown: true);
        }

        /// <summary>
        /// <paramref name="libraryVersionKnown"/> says whether <c>plc.Version</c> is the version the
        /// library was built under. It is for a build: <c>set-version</c> writes
        /// <see cref="AutomationInterface.TwinCATProjectVersion"/> of it into the plcproj. A mirror that
        /// only knows a NuGet version such as <c>1.2.3</c>, which may be a library versioned
        /// <c>1.2.3.0</c>, passes false, and the package then carries no
        /// <see cref="LibraryVersionTagKey"/> tag rather than a wrong one.
        /// </summary>
        public static string PackLibrary(
            ConfigPlcProject plc,
            string libraryFilePath,
            string target,
            bool compiled,
            string outputDirectory,
            PlcPublishMetadata metadata,
            IEnumerable<NugetPackFile> additionalFiles,
            bool libraryVersionKnown)
        {
            if (plc == null)
                throw new ArgumentNullException(nameof(plc));
            if (string.IsNullOrEmpty(libraryFilePath) || !File.Exists(libraryFilePath))
                throw new Exceptions.LibraryNotFoundException(plc.Name, plc.Version, $"Could not find library file '{libraryFilePath}'");

            metadata = metadata ?? ConfigPlcProjectFactory.DerivePublishMetadata(libraryFilePath);

            var tags = new List<string> { LibraryTag };
            if (compiled)
                tags.Add(CompiledLibraryTag);

            var versionTag = libraryVersionKnown ? LibraryVersionTag(plc.Version) : null;
            if (versionTag != null)
                tags.Add(versionTag);

            var files = new List<NugetPackFile> { new NugetPackFile { Source = libraryFilePath, Target = target } };
            if (additionalFiles != null)
                files.AddRange(additionalFiles);

            return NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = plc.Name,
                Version = plc.Version,
                Title = FirstNonEmpty(metadata.DisplayName, plc.Title, plc.Name),
                Authors = FirstNonEmpty(plc.DistributorName, metadata.Authors),
                Description = FirstNonEmpty(metadata.Description, plc.Title, plc.Name),
                Copyright = FirstNonEmpty(plc.DistributorName, metadata.Authors),
                ProjectUrl = metadata.ProjectUrl,
                License = metadata.License,
                LicenseFile = metadata.LicenseFile,
                IconFile = metadata.IconFile,
                Tags = tags,
                Dependencies = Dependencies(plc),
                Files = files,
                OutputDirectory = outputDirectory
            });
        }

        /// <summary>
        /// Packs a Beckhoff repository package (.tmx, .tml, .tme) that has no PLC library.
        /// The files keep the path under <c>repository/</c> that download copies back out.
        /// </summary>
        public static string PackRepository(
            ConfigPlcProject plc,
            IEnumerable<NugetPackFile> files,
            string outputDirectory,
            PlcPublishMetadata metadata = null)
        {
            if (plc == null)
                throw new ArgumentNullException(nameof(plc));

            metadata = metadata ?? new PlcPublishMetadata();
            return NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = plc.Name,
                Version = plc.Version,
                Title = FirstNonEmpty(metadata.DisplayName, plc.Title, plc.Name),
                Authors = FirstNonEmpty(plc.DistributorName, metadata.Authors),
                Description = FirstNonEmpty(metadata.Description, plc.Title, plc.Name),
                Copyright = FirstNonEmpty(plc.DistributorName, metadata.Authors),
                ProjectUrl = metadata.ProjectUrl,
                License = metadata.License,
                Tags = new[] { "tmx" },
                Dependencies = Dependencies(plc),
                Files = files,
                OutputDirectory = outputDirectory
            });
        }

        /// <summary>
        /// Packs a PLC application. An application is not delivered as a library but as its boot
        /// project, so the package carries the whole <c>_Boot</c> directory plus the module
        /// description (<c>.tmc</c>) that goes with it.
        /// </summary>
        public static string PackApplication(
            ConfigPlcProject plc,
            string bootDirectory,
            string tmcFilePath,
            string outputDirectory,
            PlcPublishMetadata metadata = null)
        {
            if (plc == null)
                throw new ArgumentNullException(nameof(plc));
            if (string.IsNullOrEmpty(bootDirectory) || !Directory.Exists(bootDirectory))
                throw new DirectoryNotFoundException($"Could not find boot project '{bootDirectory}' for application {plc.Name}");

            metadata = metadata ?? ConfigPlcProjectFactory.DerivePublishMetadataFromPlcProj(plc);

            var files = new List<NugetPackFile>();
            var bootRoot = Path.GetFullPath(bootDirectory);
            foreach (var file in Directory.GetFiles(bootRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetDirectoryName(file).Substring(bootRoot.Length).Trim('\\', '/');
                files.Add(new NugetPackFile
                {
                    Source = file,
                    Target = string.IsNullOrEmpty(relative) ? "_Boot" : $"_Boot/{relative.Replace('\\', '/')}"
                });
            }

            if (files.Count == 0)
                throw new FileNotFoundException($"Boot project '{bootRoot}' for application {plc.Name} is empty");

            if (!string.IsNullOrEmpty(tmcFilePath) && File.Exists(tmcFilePath))
                files.Add(new NugetPackFile { Source = tmcFilePath, Target = null });

            return NugetPackBuilder.Pack(new NugetPackRequest
            {
                Id = plc.Name,
                Version = plc.Version,
                Title = FirstNonEmpty(metadata.DisplayName, plc.Title, plc.Name),
                Authors = FirstNonEmpty(plc.DistributorName, metadata.Authors),
                Description = FirstNonEmpty(metadata.Description, plc.Title, plc.Name),
                Copyright = FirstNonEmpty(plc.DistributorName, metadata.Authors),
                ProjectUrl = metadata.ProjectUrl,
                License = metadata.License,
                LicenseFile = metadata.LicenseFile,
                IconFile = metadata.IconFile,
                Tags = new[] { ApplicationTag },
                Dependencies = Dependencies(plc),
                Files = files,
                OutputDirectory = outputDirectory
            });
        }

        static IEnumerable<NugetPackDependency> Dependencies(ConfigPlcProject plc)
        {
            return (plc.Packages ?? Enumerable.Empty<ConfigPlcPackage>())
                .Where(x => !string.IsNullOrEmpty(x?.Name))
                .Select(x => new NugetPackDependency { Id = x.Name, Version = x.Version })
                .ToList();
        }

        static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }
    }
}
