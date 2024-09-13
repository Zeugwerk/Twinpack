using System.Collections.Generic;
using Twinpack.Core;
using System.ComponentModel;
using Spectre.Console.Cli;
using System.Threading;
using System.Linq;
using Twinpack.Models;
using System;
using NuGet.Configuration;

namespace Twinpack.Commands
{
    [Description(@"Downloads package(s) using the sources defined in %APPDATA%\Zeugwerk\Twinpack\sourceRepositories.json.")]
    public class UpdateCommand : AbstractCommand<UpdateCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandOption("--project <PROJECT>")]
            [Description("The name of the project where the packages should be updated.")]
            public string ProjectName { get; set; }

            [CommandOption("--plc <PLC>")]
            [Description("The name of the PLC where the packages will be updated.")]
            public string PlcName { get; set; }

            [CommandOption("--package")]
            [Description("Specifies the package(s) to be updated, by default all will be updated")]
            public string[] Packages { get; set; }

            [CommandOption("--framework")]
            [Description("Specifies the framework(s) to be updated, by default all will be updated. Can be used without specifying single packages")]
            public string[] Frameworks { get; set; }

            [CommandOption("--version")]
            [Description("Defines the version(s) of the specified package(s). If omitted, Twinpack will automatically update to the latest available version.")]
            public string[] Versions { get; set; }

            [CommandOption("--branch")]
            [Description("Specifies the branch(es) of the package(s). If not provided, Twinpack will determine the appropriate branch automatically, i.e. 'main'")]
            public string[] Branches { get; set; }

            [CommandOption("--target")]
            [Description("Indicates the target(s) for the package(s). If omitted, Twinpack will select the appropriate target(s) automatically, i.e. 'TC3.1'")]
            public string[] Targets { get; set; }

            [CommandOption("--configuration")]
            [Description("Specifies the configuration(s) for the package(s). If not specified, Twinpack will handle the configuration automatically, i.e. 'Release'")]
            public string[] Configurations { get; set; }

            [CommandOption("--include-provided-packages")]
            [Description("Update packages, which are provided by the configuration (Plcs, which are also packages themselves)")]
            public bool IncludeProvidedPackages { get; set; }
            [CommandOption("--skip-download")]
            [Description("Skips the download of package(s)")]
            public bool SkipDownload { get; set; }
            [CommandOption("--force-download")]
            [Description("Forces the download of package(s) even if they are already available on the system.")]
            public bool ForceDownload { get; set; }
            [CommandOption("--headed")]
            [Description("Enables the use of the Beckhoff Automation Interface, which is required for installing and/or uninstalling packages on the target. In 'headless' mode, install operations have to be performed by Beckhoff's 'RepTool.exe'. Defaults to false")]
            public bool Headed { get; set; }
        }
        public override int Execute(CommandContext context, Settings settings)
        {
            if (!settings.ForceDownload && !settings.Headed)
                _logger.Warn("Using headless mode, downloading packages even if they are available on the system.");

            Initialize(settings.Headed);

            var usedPackages = _twinpack.RetrieveUsedPackagesAsync().GetAwaiter().GetResult();
            List<PackageItem> packages;
            if (settings.Packages != null || settings.Frameworks != null)
            {
                packages = usedPackages.Where(
                    x => (settings.ProjectName == null || settings.ProjectName == x.ProjectName) &&
                         (settings.ProjectName == null || settings.ProjectName == x.ProjectName) &&
                         (settings.Packages == null || settings.Packages.Any(y => y == x.Update?.Name)) &&
                         (settings.Frameworks == null || settings.Frameworks.Any(y => y == x.Update?.Framework)))
                    .Select(x => new PackageItem(x) { Package = new Protocol.Api.PackageGetResponse(x.Update), PackageVersion = x.Update }).ToList();

                foreach (var package in packages)
                {
                    var i = settings.Packages != null && package.Package.Name != null ? Array.IndexOf(settings.Packages, package.Package.Name) : -1;
                    if (i >= 0)
                    {
                        package.Config.Version = settings.Versions?.ElementAtOrDefault(i) ?? package.PackageVersion.Version;
                        package.Config.Branch = settings.Branches?.ElementAtOrDefault(i) ?? package.PackageVersion.Branch;
                        package.Config.Configuration = settings.Configurations?.ElementAtOrDefault(i) ?? package.PackageVersion.Configuration;
                        package.Config.Target = settings.Targets?.ElementAtOrDefault(i) ?? package.PackageVersion.Target;
                    }

                    i = settings.Frameworks != null && package.PackageVersion.Framework != null ? Array.IndexOf(settings.Frameworks, package.PackageVersion.Framework) : -1;
                    if (i >= 0)
                    {
                        package.Config.Version = settings.Versions?.ElementAtOrDefault(i) ?? package.PackageVersion.Version;
                        package.Config.Branch = settings.Branches?.ElementAtOrDefault(i) ?? package.PackageVersion.Branch;
                        package.Config.Configuration = settings.Configurations?.ElementAtOrDefault(i) ?? package.PackageVersion.Configuration;
                        package.Config.Target = settings.Targets?.ElementAtOrDefault(i) ?? package.PackageVersion.Target;
                    }
                }

                // force new retrievable of metadata
                foreach (var package in packages)
                {
                    package.Package = null;
                    package.PackageVersion = null;
                }
            }
            else
            {
                packages = usedPackages.Select(x => new PackageItem(x) { Package = new Protocol.Api.PackageGetResponse(x.Update), PackageVersion = x.Update }).ToList();
            }

            // update all packages
            _twinpack.UpdatePackagesAsync(
                packages,
                new TwinpackService.UpdatePackageOptions
                {
                    IncludeProvidedPackages = settings.IncludeProvidedPackages,
                    ForceDownload = settings.ForceDownload,
                    SkipDownload = settings.SkipDownload,
                    IncludeDependencies = true
                }).GetAwaiter().GetResult();
            return 0;
        }


    }
}
