using System.Collections.Generic;
using System.Linq;
using Twinpack.Configuration;
using Twinpack.Models;

namespace Twinpack.Application
{
    public partial class TwinpackService
    {
        public class DownloadPackageOptions
        {
            public bool IncludeProvidedPackages = false;
            public bool IncludeDependencies = true;
            public bool ForceDownload = false;
            public string? DownloadPath = null;
        }

        public class AddPackageOptions
        {
            public AddPackageOptions() { }
            public AddPackageOptions(AddPackageOptions rhs)
            {
                SkipDownload = rhs?.SkipDownload ?? false;
                SkipInstall = rhs?.SkipInstall ?? false;
                ForceDownload = rhs?.ForceDownload ?? false;
                UpdatePlc = rhs?.UpdatePlc ?? true;
                IncludeDependencies = rhs?.IncludeDependencies ?? true;
                DownloadPath = rhs?.DownloadPath;
            }

            public bool SkipDownload = false;
            public bool SkipInstall = false;
            public bool ForceDownload = false;
            public bool UpdatePlc = true;
            public bool IncludeDependencies = true;
            public string? DownloadPath = null;
        }

        public class RestorePackageOptions : AddPackageOptions
        {
            public bool IncludeProvidedPackages = false;
            public List<string> ExcludedPackages;
        }

        public class UpdatePackageFilters
        {
            public string ProjectName;
            public string PlcName;
            public string[] Packages;
            public string[] Frameworks;
            public string[] Versions;
            public string[] Branches;
            public string[] Configurations;
            public string[] Targets;
        }

        public class UpdatePackageOptions : AddPackageOptions
        {
            public bool IncludeProvidedPackages = false;
        }

        public class SetPackageVersionOptions
        {
            public bool PurgePackages = false;
            public string? ProjectName;
            public string? PlcName;
            public bool SyncFrameworkPackages;
            public string? PreferredFrameworkBranch;
            public string? PreferredFrameworkTarget;
            public string? PreferredFrameworkConfiguration;
        }

        /// <summary>
        /// One PLC project within a <see cref="Config"/> while running set-version / framework sync:
        /// exposes the PLC as a logical package (same name as the PLC) plus its declared library version.
        /// </summary>
        public class VersionedPlcProjectScope
        {
            public VersionedPlcProjectScope(Config config, ConfigPlcProject plc)
            {
                Config = config;
                Plc = plc;
            }

            public Config Config;
            public ConfigPlcProject Plc;

            /// <summary>PLC name when the PLC is also published/consumed as a package (framework identity).</summary>
            public string PlcAsPackageName
            {
                get => Plc.Name;
                set => Plc.Name = value;
            }

            /// <summary>Version string stored on the PLC project (library version line).</summary>
            public string DeclaredVersion
            {
                get => Plc.Version;
                set => Plc.Version = value;
            }

            /// <summary>Minimal persisted reference for this PLC when it is treated as its own package (framework identity).</summary>
            public PlcPackageReference AsOwnPackageReference =>
                new PlcPackageReference
                {
                    Name = PlcAsPackageName
                };

            public PackageItem PackageItem =>
                new PackageItem
                {
                    ProjectName = Plc.ProjectName,
                    PlcName = Plc.Name,
                    PlcPackageReference = AsOwnPackageReference
                };

            public List<PackageItem> AffectedPackages =>
                Plc.Packages.Select(y => new PackageItem
                {
                    ProjectName = Plc.ProjectName,
                    PlcName = Plc.Name,
                    PlcPackageReference = new PlcPackageReference { Name = y.Name }
                })
                .Prepend(PackageItem)
                .ToList();

            public List<PackageItem> Packages =>
                Plc.Packages.Select(y => new PackageItem
                {
                    ProjectName = Plc.ProjectName,
                    PlcName = Plc.Name,
                    PlcPackageReference = y
                })
                .ToList();
        }
    }
}
