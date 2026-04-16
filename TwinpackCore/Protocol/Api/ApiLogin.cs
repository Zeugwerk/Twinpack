using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Twinpack.Protocol.Api
{
    /// <summary>Successful Twinpack login payload (user, token, publisher axes, capability flags, optional client update hints).</summary>
    public class TwinpackLoginResult : Response
    {
        /// <summary>One Twinpack login configuration axis (JSON: configurations[].configuration).</summary>
        public class ConfigurationOption
        {
            [JsonPropertyName("configuration")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }
            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPublic { get { return Public == 1; } }

            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPrivate { get { return Public == 0; } }
        }

        /// <summary>One Twinpack login target axis (JSON: targets[].target).</summary>
        public class TargetOption
        {
            [JsonPropertyName("target")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPublic { get { return Public == 1; } }
            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPrivate { get { return Public == 0; } }
        }

        /// <summary>One Twinpack login entitlement axis (JSON: entitlements[].entitlement).</summary>
        public class EntitlementOption
        {
            [JsonPropertyName("entitlement")]
            public string Name { get; set; }

            [JsonPropertyName("public")]
            public int Public { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPublic { get { return Public == 1; } }
            [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
            public bool IsPrivate { get { return Public == 0; } }
        }

        [JsonPropertyName("user")]
        public string User { get; set; }
        [JsonPropertyName("token")]
        public string Token { get; set; }
        [JsonPropertyName("distributor-name")]
        public string DistributorName { get; set; }
        [JsonPropertyName("configurations")]
        public List<ConfigurationOption> Configurations { get; set; }
        [JsonPropertyName("targets")]
        public List<TargetOption> Targets { get; set; }
        [JsonPropertyName("entitlements")]
        public List<EntitlementOption> Entitlements { get; set; }
        [JsonPropertyName("flags")]
        public List<string> Flags { get; set; }
        [JsonPropertyName("update-version")]
        public string UpdateVersion { get; set; }
        [JsonPropertyName("update-url")]
        public string UpdateUrl { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasCompiledLibraryCapability { get { return Flags?.Contains("FLAG_COMPILED") == true; } }
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool HasLicenseTmcCapability { get { return Flags?.Contains("FLAG_RUNTIME_LICENSE") == true; } }
    }
}
