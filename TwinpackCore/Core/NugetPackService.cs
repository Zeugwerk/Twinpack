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
            PlcPublishMetadata metadata = null)
        {
            if (plc == null)
                throw new ArgumentNullException(nameof(plc));
            if (string.IsNullOrEmpty(libraryFilePath) || !File.Exists(libraryFilePath))
                throw new Exceptions.LibraryNotFoundException(plc.Name, plc.Version, $"Could not find library file '{libraryFilePath}'");

            metadata = metadata ?? ConfigPlcProjectFactory.DerivePublishMetadata(libraryFilePath);

            var tags = new List<string> { LibraryTag };
            if (compiled)
                tags.Add(CompiledLibraryTag);

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
                Files = new[] { new NugetPackFile { Source = libraryFilePath, Target = target } },
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
