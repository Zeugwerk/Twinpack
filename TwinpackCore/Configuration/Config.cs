using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Twinpack.Models;
using Twinpack.Protocol.Api;

namespace Twinpack.Configuration
{
    // This class is used for deserializing a json config file
    public class Config
    {        
        public Config()
        {
            Fileversion = LatestVersion;
            Projects = new List<ConfigProject>();
            Modules = new List<string>();
        }

        [JsonPropertyName("fileversion")]
        public int Fileversion { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("solution")]
        public String Solution { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("projects")]
        public List<ConfigProject> Projects { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("modules")]
        public List<string> Modules { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public const int LatestVersion = 2;
        
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public String WorkingDirectory { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string FilePath { get; set; }
    }

    public enum ProjectFileType
    {
        Plc, // tsproj / tspproj
        Hmi,  // hmiproj
        Database // tcconnprj
    }

    public class ConfigProject
    {
        public ConfigProject()
        {
            Plcs = new List<ConfigPlcProject>();
            Type = ProjectFileType.Plc;
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [DefaultValue(ProjectFileType.Plc)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("type")]
        public ProjectFileType Type { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("plcs")]
        public List<ConfigPlcProject> Plcs { get; set; }

        /// <summary>TF2000 runtime service instance folder name under ProgramData\...\service\.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("server-instance")]
        public string ServerInstance { get; set; }
    }

    [JsonConverter(typeof(ConfigPlcPackageConverter))]
    public class ConfigPlcPackage
    {
        /// <summary>
        /// Whether a configured namespace can be handed to TwinCAT at all. Only an identifier is accepted,
        /// anything else is reported as C0416 and takes every qualified access to the library down with it.
        /// Configurations written before Twinpack 1.7.17.0 stamped the package name in here, which nothing
        /// read back then, so names like "SPT Base Types" or "TwinCAT.XAE.PLC.Lib.Tc2_System" are still
        /// sitting in older config.json files and have to be ignored rather than passed on.
        /// </summary>
        public static bool IsUsableNamespace(string ns)
        {
            return !string.IsNullOrEmpty(ns) && Regex.IsMatch(ns, @"^[A-Za-z_][A-Za-z0-9_]*$");
        }

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
            Namespace = null;
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
            Namespace = pv.Namespace;
            Parameters = pv.Parameters;
            Options = pv.Options;
        }

        public const string DefaultBranch = "main";
        public const string DefaultTarget = "TC3.1";
        public const string DefaultConfiguration = "Release";

        public ConfigPlcPackage()
        {
            Name = "";
            Branch = DefaultBranch;
            Target = DefaultTarget;
            Configuration = DefaultConfiguration;
            Version = null;
            DistributorName = null;
            Namespace = null;
            Parameters = null;
            Options = null;
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [DefaultValue(DefaultBranch)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("branch")]
        public string Branch { get; set; }

        [DefaultValue(DefaultTarget)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("target")]
        public string Target { get; set; }
        
        [DefaultValue(DefaultConfiguration)]
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

    /// <summary>
    /// System.Text.Json ignores <see cref="DefaultValueAttribute"/> and only skips the CLR
    /// default, so branch "main", target "TC3.1" and configuration "Release" would always be
    /// written. This converter omits those configured defaults from config.json and still
    /// restores them when a file leaves the fields out.
    /// </summary>
    public sealed class ConfigPlcPackageConverter : JsonConverter<ConfigPlcPackage>
    {
        private sealed class Member
        {
            public PropertyInfo Property;
            public string Name;
            public JsonIgnoreCondition? Condition;
            public object DefaultValue;
            public bool HasDefaultValue;
        }

        private static readonly Member[] Members = typeof(ConfigPlcPackage)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.MetadataToken)
            .Select(property =>
            {
                var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
                var configuredDefault = property.GetCustomAttribute<DefaultValueAttribute>();
                return new Member
                {
                    Property = property,
                    Name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name,
                    Condition = ignore == null ? (JsonIgnoreCondition?)null : ignore.Condition,
                    DefaultValue = configuredDefault == null ? null : configuredDefault.Value,
                    HasDefaultValue = configuredDefault != null
                };
            })
            .ToArray();

        public override ConfigPlcPackage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (var document = JsonDocument.ParseValue(ref reader))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new JsonException("A package entry must be a JSON object.");

                var package = new ConfigPlcPackage();
                var ignoreCase = options.PropertyNameCaseInsensitive;
                foreach (var member in Members)
                {
                    JsonElement element;
                    if (!TryGetProperty(document.RootElement, member.Name, ignoreCase, out element))
                        continue;

                    member.Property.SetValue(package, JsonSerializer.Deserialize(element.GetRawText(), member.Property.PropertyType, options));
                }

                return package;
            }
        }

        public override void Write(Utf8JsonWriter writer, ConfigPlcPackage value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var member in Members)
            {
                var current = member.Property.GetValue(value);
                if (member.HasDefaultValue
                    && member.Condition == JsonIgnoreCondition.WhenWritingDefault
                    && ValuesEqual(member.DefaultValue, current))
                {
                    current = null;
                }

                var condition = member.Condition ?? options.DefaultIgnoreCondition;
                if (ShouldSkip(condition, current, member.Property.PropertyType))
                    continue;

                writer.WritePropertyName(member.Name);
                JsonSerializer.Serialize(writer, current, member.Property.PropertyType, options);
            }

            writer.WriteEndObject();
        }

        private static bool TryGetProperty(JsonElement obj, string name, bool ignoreCase, out JsonElement value)
        {
            if (!ignoreCase)
                return obj.TryGetProperty(name, out value);

            foreach (var property in obj.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default(JsonElement);
            return false;
        }

        private static bool ValuesEqual(object expected, object actual)
        {
            if (expected == null)
                return actual == null;
            if (actual == null)
                return false;

            var expectedText = expected as string;
            var actualText = actual as string;
            if (expectedText != null && actualText != null)
                return string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase);

            return expected.Equals(actual);
        }

        private static bool ShouldSkip(JsonIgnoreCondition condition, object value, Type type)
        {
            switch (condition)
            {
                case JsonIgnoreCondition.Always:
                    return true;
                case JsonIgnoreCondition.Never:
                    return false;
                case JsonIgnoreCondition.WhenWritingNull:
                    return value == null;
                case JsonIgnoreCondition.WhenWritingDefault:
                    return IsClrDefault(value, type);
                default:
                    return false;
            }
        }

        private static bool IsClrDefault(object value, Type type)
        {
            if (value == null)
                return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
            if (!type.IsValueType)
                return false;

            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return value.Equals(Activator.CreateInstance(underlying));
        }
    }

    // This class is used for deserializing a json config file
    public class ConfigPlcProject
    {
        public ConfigPlcProject()
        {
            Version = "1.0.0.0";
            References = new Dictionary<string, List<string>>();
            Packages = new List<ConfigPlcPackage>();
            Bindings = new Dictionary<string, List<string>>();
            Patches = new ConfigPatches();
            DistributorName = "";
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

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("title")]
        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Title { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("packages")]
        public List<ConfigPlcPackage> Packages { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("references")]
        public Dictionary<String, List<String>> References { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("bindings")]
        public Dictionary<String, List<String>> Bindings { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("patches")]
        public ConfigPatches Patches { get; set; }

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

}
