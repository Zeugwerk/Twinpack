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
            When = pv.Config?.When;
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
            When = null;
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
            When = pv.When;
            WhenResolved = pv.WhenResolved;
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
            When = null;
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

        /// <summary>
        /// Product-line filter. The <see cref="Target"/> pin is unchanged.
        /// Omitted when there is nothing to filter.
        /// </summary>
        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("when")]
        public ConfigWhen When { get; set; }

        /// <summary>
        /// The dependency walk decided <see cref="When"/>, including clearing it because another
        /// parent has no restriction. Not stored in config.json.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool WhenResolved { get; set; }
        
    }

    /// <summary>
    /// Allow-list of TwinCAT targets. Each entry is an exact product line or a NuGet-style range
    /// over the product-line build, such as <c>[TC3.1.4026,)</c>.
    /// </summary>
    public class ConfigWhen
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("target")]
        public List<string> Target { get; set; }

        /// <summary>
        /// No <paramref name="when"/>, or an empty target list, matches every solution.
        /// A restriction does not match when the solution line is unknown.
        /// </summary>
        public static bool Applies(ConfigWhen when, string solutionProductLine)
        {
            return when == null || when.Matches(solutionProductLine);
        }

        /// <summary>
        /// Product line of a build's <c>--tcversion</c>. <c>TC3.1.4024.59</c> is <c>TC3.1.4024</c>.
        /// <c>TC3.1</c> names no build, so the result is null and a restriction does not match.
        /// </summary>
        public static string BuildProductLine(string requestedTcVersion)
        {
            var line = Twinpack.Core.TwinCatTargets.ProductLine(requestedTcVersion);
            if (string.IsNullOrEmpty(line) || Twinpack.Core.TwinCatTargets.IsUnpinned(line))
                return null;
            return line;
        }

        /// <summary>
        /// Same rule as <see cref="Applies(ConfigWhen, string)"/> for a version that may still
        /// carry a patch, such as <c>TC3.1.4024.59</c>.
        /// </summary>
        public static bool AppliesToBuild(ConfigWhen when, string requestedTcVersion)
        {
            return Applies(when, BuildProductLine(requestedTcVersion));
        }

        /// <summary>
        /// A <c>when.target</c> that names a build, such as <c>[TC3.1.4026,)</c>, cannot be
        /// checked against <c>TC3.1</c>. Returns the spec that needs more parts, or null.
        /// The patch (a fourth part) is ignored. <c>TC3.1.4024.56</c> is specific enough.
        /// </summary>
        public string UnevaluableSpec(string requestedTcVersion)
        {
            if (Target == null || Target.Count == 0)
                return null;

            var have = VersionDepth(requestedTcVersion);
            foreach (var entry in Target)
            {
                if (SpecDepth(entry) > have)
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// Throws when <paramref name="config"/> has a restriction the requested version cannot decide.
        /// </summary>
        public static void EnsureCheckable(Config config, string requestedTcVersion)
        {
            if (config?.Projects == null)
                return;

            foreach (var plc in config.Projects.SelectMany(project => project?.Plcs ?? Enumerable.Empty<ConfigPlcProject>()))
            {
                foreach (var package in plc?.Packages ?? Enumerable.Empty<ConfigPlcPackage>())
                    Reject(requestedTcVersion, package?.Name, package?.When);
                foreach (var reference in plc?.SystemReferences ?? Enumerable.Empty<ConfigSystemReference>())
                    Reject(requestedTcVersion, reference?.Name, reference?.When);
                foreach (var patch in plc?.Patches?.Target ?? Enumerable.Empty<ConfigTargetPatch>())
                    Reject(requestedTcVersion, patch?.Path, patch?.When);
            }
        }

        static void Reject(string requestedTcVersion, string owner, ConfigWhen when)
        {
            var spec = when?.UnevaluableSpec(requestedTcVersion);
            if (spec == null)
                return;

            var passed = string.IsNullOrWhiteSpace(requestedTcVersion) ? "(none)" : requestedTcVersion.Trim();
            var where = string.IsNullOrWhiteSpace(owner) ? "" : " on " + owner.Trim();
            throw new InvalidOperationException(
                "Cannot check when.target \"" + spec + "\"" + where + " with --tcversion " + passed + ". Pass TC3.1.4024 or TC3.1.4026.");
        }

        static int SpecDepth(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return 0;

            var spec = entry.Trim();
            if (spec.Length >= 2
                && (spec[0] == '[' || spec[0] == '(')
                && (spec[spec.Length - 1] == ']' || spec[spec.Length - 1] == ')'))
            {
                var inner = spec.Substring(1, spec.Length - 2);
                var comma = inner.IndexOf(',');
                if (comma < 0)
                    return 0;

                return Math.Max(VersionDepth(inner.Substring(0, comma)), VersionDepth(inner.Substring(comma + 1)));
            }

            return VersionDepth(spec);
        }

        /// <summary>Counts <c>3.1.4024</c> as 3. A patch does not add depth.</summary>
        static int VersionDepth(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            var body = text.Trim();
            if (body.StartsWith("TC", StringComparison.OrdinalIgnoreCase))
                body = body.Substring(2);

            var depth = 0;
            foreach (var part in body.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part, out _))
                    break;
                depth++;
                if (depth == 3)
                    break;
            }

            return depth;
        }

        public bool Matches(string solutionProductLine)
        {
            if (Target == null || Target.Count == 0)
                return true;

            var line = Twinpack.Core.TwinCatTargets.ProductLine(solutionProductLine);
            if (string.IsNullOrEmpty(line))
                return false;

            foreach (var entry in Target)
            {
                if (EntryMatches(entry, line))
                    return true;
            }

            return false;
        }

        static bool EntryMatches(string entry, string solutionLine)
        {
            if (string.IsNullOrWhiteSpace(entry))
                return false;

            var spec = entry.Trim();
            if (spec.Length >= 2
                && (spec[0] == '[' || spec[0] == '(')
                && (spec[spec.Length - 1] == ']' || spec[spec.Length - 1] == ')'))
            {
                return RangeMatches(spec, solutionLine);
            }

            return string.Equals(Twinpack.Core.TwinCatTargets.ProductLine(spec), solutionLine, StringComparison.OrdinalIgnoreCase);
        }

        static bool RangeMatches(string spec, string solutionLine)
        {
            var minInclusive = spec[0] == '[';
            var maxInclusive = spec[spec.Length - 1] == ']';
            var inner = spec.Substring(1, spec.Length - 2);
            var comma = inner.IndexOf(',');
            if (comma < 0)
                return false;

            var minText = inner.Substring(0, comma).Trim();
            var maxText = inner.Substring(comma + 1).Trim();
            if (minText.Length == 0 && maxText.Length == 0)
                return false;

            if (!TryGetBuild(solutionLine, out var family, out var build))
                return false;

            if (minText.Length != 0)
            {
                if (!TryGetBuild(Twinpack.Core.TwinCatTargets.ProductLine(minText), out var minFamily, out var minBuild))
                    return false;
                if (!string.Equals(family, minFamily, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (build < minBuild || (build == minBuild && !minInclusive))
                    return false;
            }

            if (maxText.Length != 0)
            {
                if (!TryGetBuild(Twinpack.Core.TwinCatTargets.ProductLine(maxText), out var maxFamily, out var maxBuild))
                    return false;
                if (!string.Equals(family, maxFamily, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (build > maxBuild || (build == maxBuild && !maxInclusive))
                    return false;
            }

            return true;
        }

        public static ConfigWhen Copy(ConfigWhen when)
        {
            if (when?.Target == null || when.Target.Count == 0)
                return null;

            return new ConfigWhen { Target = new List<string>(when.Target) };
        }

        /// <summary>
        /// <paramref name="current"/> is the when gathered from earlier parents.
        /// An unrestricted parent clears it for good (<paramref name="unrestricted"/>).
        /// A restricted parent unions its allow-list.
        /// </summary>
        public static ConfigWhen Inherit(ConfigWhen current, ConfigWhen parent, ref bool unrestricted)
        {
            if (unrestricted)
                return null;

            if (parent?.Target == null || parent.Target.Count == 0)
            {
                unrestricted = true;
                return null;
            }

            if (current?.Target == null || current.Target.Count == 0)
                return Copy(parent);

            var targets = new List<string>(current.Target);
            foreach (var entry in parent.Target)
            {
                if (!targets.Any(x => string.Equals(x, entry, StringComparison.OrdinalIgnoreCase)))
                    targets.Add(entry);
            }

            return new ConfigWhen { Target = targets };
        }

        static bool TryGetBuild(string productLine, out string family, out int build)
        {
            family = null;
            build = 0;
            if (string.IsNullOrWhiteSpace(productLine))
                return false;

            var body = productLine.Trim();
            if (body.StartsWith("TC", StringComparison.OrdinalIgnoreCase))
                body = body.Substring(2);

            var parts = body.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !int.TryParse(parts[2], out build))
                return false;

            family = parts[0] + "." + parts[1];
            return true;
        }
    }

    /// <summary>
    /// A TwinCAT system library stored in <c>system-references</c>.
    /// <see cref="DistributorName"/> is omitted when empty. The legacy <c>Name=Version</c> map has no distributor.
    /// </summary>
    public class ConfigSystemReference
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }

        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("when")]
        public ConfigWhen When { get; set; }

        /// <summary>
        /// Turns a legacy <c>references</c> map into <see cref="ConfigPlcProject.SystemReferences"/>
        /// and clears the map so the next save writes only <c>system-references</c>.
        /// A project that already has <c>system-references</c> keeps that list and drops the map.
        /// </summary>
        public static void AdoptLegacyReferences(ConfigPlcProject plc)
        {
            if (plc == null)
                return;

            if (plc.SystemReferences != null)
            {
                plc.References = null;
                return;
            }

            var legacy = plc.References;
            plc.References = null;
            if (legacy == null || legacy.Count == 0)
                return;

            var converted = ConvertLegacyReferences(legacy);
            plc.SystemReferences = converted.Count == 0 ? null : converted;
        }

        static List<ConfigSystemReference> ConvertLegacyReferences(Dictionary<string, List<string>> legacy)
        {
            var parsed = new List<LegacyReference>();
            foreach (var pair in legacy)
            {
                var key = pair.Key ?? "";
                var everyLine = AppliesToEveryLine(key);
                var fromStar = key.Trim() == "*";
                var line = everyLine ? null : Twinpack.Core.TwinCatTargets.ProductLine(key);
                if (!everyLine && string.IsNullOrEmpty(line))
                    everyLine = true;

                foreach (var entry in pair.Value ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(entry))
                        continue;

                    var eq = entry.IndexOf('=');
                    var name = (eq >= 0 ? entry.Substring(0, eq) : entry).Trim();
                    var version = eq >= 0 ? entry.Substring(eq + 1).Trim() : "*";
                    if (name.Length == 0)
                        continue;
                    if (version.Length == 0)
                        version = "*";

                    parsed.Add(new LegacyReference
                    {
                        Name = name,
                        Version = version,
                        EveryLine = everyLine,
                        FromStar = fromStar,
                        Line = line
                    });
                }
            }

            var adopted = new List<ConfigSystemReference>();
            foreach (var group in parsed.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                var star = group.FirstOrDefault(x => x.FromStar);
                var universal = group.FirstOrDefault(x => x.EveryLine);
                var chosen = star ?? universal ?? group.First();
                ConfigWhen when = null;
                if (star == null && universal == null && !string.IsNullOrEmpty(chosen.Line))
                {
                    when = new ConfigWhen { Target = new List<string> { chosen.Line } };
                }

                adopted.Add(new ConfigSystemReference
                {
                    Name = chosen.Name,
                    Version = chosen.Version,
                    When = when
                });
            }

            return adopted;
        }

        static bool AppliesToEveryLine(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return true;

            var trimmed = key.Trim();
            return trimmed == "*"
                || string.Equals(trimmed, ConfigPlcPackage.DefaultTarget, StringComparison.OrdinalIgnoreCase);
        }

        sealed class LegacyReference
        {
            public string Name;
            public string Version;
            public bool EveryLine;
            public bool FromStar;
            public string Line;
        }
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
        [JsonPropertyName("system-references")]
        public List<ConfigSystemReference> SystemReferences { get; set; }

        /// <summary>
        /// Legacy <c>Name=Version</c> map. Deserialization only. Cleared once
        /// <see cref="SystemReferences"/> has been filled, so the next save omits it.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("references")]
        public Dictionary<String, List<String>> References { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("bindings")]
        public Dictionary<String, List<String>> Bindings { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("patches")]
        public ConfigPatches Patches { get; set; }

        public IEnumerable<ConfigSystemReference> ApplicableSystemReferences(string solutionProductLine)
        {
            return (SystemReferences ?? Enumerable.Empty<ConfigSystemReference>())
                .Where(reference => reference != null && ConfigWhen.Applies(reference.When, solutionProductLine));
        }

    }

    /// <summary>
    /// One file applied for a TwinCAT target. <see cref="When"/> is omitted when the file applies to every line.
    /// </summary>
    public class ConfigTargetPatch
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [DefaultValue(null)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("when")]
        public ConfigWhen When { get; set; }

        /// <summary>
        /// Turns a legacy <c>patches.platform</c> map into <see cref="ConfigPatches.Target"/>
        /// and clears the map so the next save writes <c>target</c>.
        /// A <c>target</c> list that is already present is kept, and the map is dropped.
        /// </summary>
        public static void AdoptLegacyPlatform(ConfigPatches patches)
        {
            if (patches == null)
                return;

            if (patches.Target != null)
            {
                patches.Platform = null;
                return;
            }

            var legacy = patches.Platform;
            patches.Platform = null;
            if (legacy == null || legacy.Count == 0)
                return;

            var converted = ConvertLegacyPlatform(legacy);
            patches.Target = converted.Count == 0 ? null : converted;
        }

        static List<ConfigTargetPatch> ConvertLegacyPlatform(IDictionary<string, List<string>> legacy)
        {
            var parsed = new List<LegacyPlatformPatch>();
            foreach (var pair in legacy)
            {
                var key = pair.Key ?? "";
                var everyLine = AppliesToEveryLine(key);
                var fromStar = key.Trim() == "*";
                var line = everyLine ? null : Twinpack.Core.TwinCatTargets.ProductLine(key);
                if (!everyLine && string.IsNullOrEmpty(line))
                    everyLine = true;

                foreach (var entry in pair.Value ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(entry))
                        continue;

                    parsed.Add(new LegacyPlatformPatch
                    {
                        Path = entry.Trim(),
                        EveryLine = everyLine,
                        FromStar = fromStar,
                        Line = line
                    });
                }
            }

            var adopted = new List<ConfigTargetPatch>();
            foreach (var group in parsed.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
            {
                var star = group.FirstOrDefault(x => x.FromStar);
                var universal = group.FirstOrDefault(x => x.EveryLine);
                ConfigWhen when = null;
                if (star == null && universal == null)
                {
                    var lines = group
                        .Select(x => x.Line)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (lines.Count > 0)
                        when = new ConfigWhen { Target = lines };
                }

                adopted.Add(new ConfigTargetPatch
                {
                    Path = (star ?? universal ?? group.First()).Path,
                    When = when
                });
            }

            return adopted;
        }

        static bool AppliesToEveryLine(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return true;

            var trimmed = key.Trim();
            return trimmed == "*"
                || string.Equals(trimmed, ConfigPlcPackage.DefaultTarget, StringComparison.OrdinalIgnoreCase);
        }

        sealed class LegacyPlatformPatch
        {
            public string Path;
            public bool EveryLine;
            public bool FromStar;
            public string Line;
        }
    }

    public class ConfigPatches
    {
        public ConfigPatches()
        {
            Platform = new ConfigPlatformPatches();
            Argument = new ConfigArgumentPatch();
            License = new ConfigLicensePatches();
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("target")]
        public List<ConfigTargetPatch> Target { get; set; }

        /// <summary>
        /// Legacy platform map. Deserialization only. Cleared once <see cref="Target"/> has been filled.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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
