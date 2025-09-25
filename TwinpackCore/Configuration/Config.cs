using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace Twinpack.Configuration
{
    // This class is used for deserializing a json config file
    public class Config
    {
        public Config()
        {
            Fileversion = 1;
            Projects = new List<ConfigProject>();
        }

        [JsonPropertyName("fileversion")]
        public int Fileversion { get; set; }

        [JsonPropertyName("solution")]
        public String Solution { get; set; }

        [JsonPropertyName("projects")]
        public List<ConfigProject> Projects { get; set; }

        [JsonPropertyName("modules")]
        public List<string> Modules { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public String WorkingDirectory { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string FilePath { get; set; }

        [JsonIgnore]
        public bool IsComposite => Modules?.Count > 0;

        [JsonIgnore]
        public bool IsLeaf => !IsComposite;

        public void Validate()
        {
            if (IsComposite && (!string.IsNullOrEmpty(Solution) || (Projects?.Count ?? 0) > 0))
                throw new InvalidOperationException("Composite configs cannot define solution or projects.");
        }
    }

    public class ConfigProject
    {
        public ConfigProject()
        {
            Plcs = new List<ConfigPlcProject>();
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("plcs")]
        public List<ConfigPlcProject> Plcs { get; set; }
    }

    public class ConfigPlcPackage
    {
        public ConfigPlcPackage(PackageItem pv)
        {
            Name = pv.PackageVersion?.Name ?? pv.Package?.Name ?? pv.Config?.Name ?? pv.Catalog?.Name;
            DistributorName = pv.PackageVersion?.DistributorName ?? pv.Package?.DistributorName ?? pv.Config?.DistributorName ?? pv.Catalog?.DistributorName;

            Version = pv.PackageVersion?.Version;
            Branch = pv.PackageVersion?.Branch;
            Target = pv.PackageVersion?.Target;
            Configuration = pv.PackageVersion?.Configuration;

            Namespace = pv.Config?.Namespace;
            Parameters = pv.Config?.Parameters;
            Options = pv.Config?.Options;
        }

        public ConfigPlcPackage(PackageVersionGetResponse pv)
        {
            Version = pv.Version;
            Name = pv.Name;
            Branch = pv.Branch;
            Target = pv.Target;
            Configuration = pv.Configuration;
            DistributorName = pv.DistributorName;
            Namespace = pv.Name;
            Parameters = null;
            Options = null;
        }

        public ConfigPlcPackage(ConfigPlcPackage pv)
        {
            Version = pv.Version;
            Name = pv.Name;
            Branch = pv.Branch;
            Target = pv.Target;
            Configuration = pv.Configuration;
            DistributorName = pv.DistributorName;
            Namespace = pv.Name;
            Parameters = pv.Parameters;
            Options = pv.Options;
        }

        public ConfigPlcPackage()
        {
            Name = "";
            Branch = "main";
            Target = "TC3.1";
            Configuration = "Release";
            Version = null;
            DistributorName = null;
            Namespace = null;
            Parameters = null;
            Options = null;
        }
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [DefaultValue("main")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("branch")]
        public string Branch { get; set; }

        [DefaultValue("TC3.1")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("target")]
        public string Target { get; set; }
        
        [DefaultValue("Release")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("configuration")]
        public string Configuration { get; set; }
        
        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]        
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }
        
        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]        
        [JsonPropertyName("namespace")]
        public string Namespace { get; set; }

        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; }

        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("options")]
        public AddPlcLibraryOptions Options { get; set; }
        
    }

    // This class is used for deserializing a json config file
    public class ConfigPlcProject
    {
        public ConfigPlcProject()
        {
            Version = "1.0.0.0";
            Frameworks = new ConfigFrameworks();
            References = new Dictionary<string, List<string>>();
            Repositories = new List<string>();
            Packages = new List<ConfigPlcPackage>();
            Bindings = new Dictionary<string, List<string>>();
            Patches = new ConfigPatches();
            Description = "";
            IconFile = "";
            DisplayName = "";
            DistributorName = "";
            ProjectUrl = "";
            Authors = "";
            Entitlement = "";
            License = "";
            LicenseFile = "";
            LicenseTmcFile = "";
        }

        public enum PlcProjectType
        {
            FrameworkLibrary, // Framework Library
            Application, // Activateable PLC
            Library, // Library with a Framework independent versionnumber
            UnitTestApplication // Activatable PLC, which is only meant for unittests
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public String RootPath { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public String ProjectName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public String FilePath { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public PlcProjectType PlcType {
            get
            {
                if (Type != null)
                {
                    switch (Type.ToLower())
                    {
                        case "app":
                        case "application":
                            return PlcProjectType.Application;
                        case "productlibrary":
                        case "library":
                            return PlcProjectType.Library;
                        case "unittestapplication":
                            return PlcProjectType.UnitTestApplication;                            
                    }
                }

                return PlcProjectType.FrameworkLibrary;
            }
        }
        
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("title")]
        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Title { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("frameworks")]
        public ConfigFrameworks Frameworks { get; set; }

        [JsonPropertyName("packages")]
        public List<ConfigPlcPackage> Packages { get; set; }

        [JsonPropertyName("references")]
        public Dictionary<String, List<String>> References { get; set; }

        [JsonPropertyName("repositories")]
        public List<String> Repositories { get; set; }

        [JsonPropertyName("bindings")]
        public Dictionary<String, List<String>> Bindings { get; set; }

        [JsonPropertyName("patches")]
        public ConfigPatches Patches { get; set; }

        [JsonPropertyName("description")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Description { get; set; }

        [JsonPropertyName("icon-file")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string IconFile { get; set; }        

        [JsonPropertyName("project-url")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ProjectUrl { get; set; } 
        
        [JsonPropertyName("display-name")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string DisplayName { get; set; }  

        [JsonPropertyName("authors")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Authors { get; set; }

        [JsonPropertyName("entitlement")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Entitlement { get; set; }

        [JsonPropertyName("license")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string License { get; set; }

        [JsonPropertyName("license-file")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string LicenseFile { get; set; }
        
        [JsonPropertyName("license-tmc-file")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string LicenseTmcFile { get; set; }

        [JsonPropertyName("binary-download-url")]
        [DefaultValue("")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string BinaryDownloadUrl { get; set; }     
    }

    public class ConfigPatches
    {
        public ConfigPatches()
        {
            Platform = new ConfigPlatformPatches();
            Argument = new ConfigArgumentPatch();
            License = new ConfigLicensePatches();
        }

        [JsonPropertyName("platform")]
        public ConfigPlatformPatches Platform { get; set; }

        [JsonPropertyName("argument")]
        public ConfigArgumentPatch Argument { get; set; }

        [JsonPropertyName("license")]
        public ConfigLicensePatches License { get; set; }
    }

    public class ConfigLicensePatch
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("oem-id")]
        public string OemId { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; }
    }

    public class ConfigLicensePatches : Dictionary<string, List<ConfigLicensePatch>>
    {
        public ConfigLicensePatches() : base() { }
        public ConfigLicensePatches(Dictionary<string, List<ConfigLicensePatch>> patches) : base(patches) { }

        public List<ConfigLicensePatch> Patches(string argument)
        {
            if (argument != null && ContainsKey(argument))
                return this[argument];

            return null;
        }
    }

    public class ConfigPlatformPatches : Dictionary<string, List<string>>
    {
        public ConfigPlatformPatches() : base() { }
        public ConfigPlatformPatches(IDictionary<string, List<string>> patches) : base(patches) { }

        public List<string> Patches(string platform)
        {
            if (platform != null && ContainsKey(platform))
                return this[platform];

            return null;
        }
    }

    public class ConfigArgumentPatch : Dictionary<string, List<string>>
    {
        public ConfigArgumentPatch() : base() { }
        public ConfigArgumentPatch(IDictionary<string, List<string>> patches) : base(patches) { }

        public List<string> Patches(string argument)
        {
            if (argument != null && ContainsKey(argument))
                return this[argument];

            return null;
        }
    }

    public class ConfigFrameworks : Dictionary<string, ConfigFramework>
    {
        public ConfigFrameworks() : base() { }
        public ConfigFrameworks(IDictionary<string, ConfigFramework> frameworks) : base(frameworks) { }
        public ConfigFramework Zeugwerk
        {
            get
            {
                if (ContainsKey("zeugwerk"))
                    return this["zeugwerk"];

                var Zeugwerk = new ConfigFramework();
                Zeugwerk.Repositories = new List<String> { ConfigFactory.DefaultRepository };

                return null;
            }
            set
            {
                this["zeugwerk"] = value;
            }
        }
    }

    public class ConfigFramework
    {
        public ConfigFramework()
        {
            Version = "";
            References = new List<string>();
            Repositories = new List<string>();
            Hide = false;
            QualifiedOnly = true;
        }

        [JsonPropertyName("version")]
        public String Version { get; set; }

        [JsonPropertyName("references")]
        public List<String> References { get; set; }

        [JsonPropertyName("repositories")]
        public List<String> Repositories { get; set; }

        [JsonPropertyName("hide")]
        public bool Hide { get; set; }

        [JsonPropertyName("qualified-only")]
        public bool QualifiedOnly { get; set; }
    }
}
