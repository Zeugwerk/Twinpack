﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Twinpack.Models
{
    public class AddPlcLibraryOptions
    {
        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("hide")]
        public bool HideWhenReferencedAsDependency { get; set; }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("publish-all-symbols")]
        public bool PublishSymbolsInContainer { get; set; }

        [DefaultValue(false)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("qualified-only")]
        public bool QualifiedOnly { get; set; }
    }
    public class PlcLibrary
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string DistributorName { get; set; }
        public AddPlcLibraryOptions Options { get; set; }
        public static bool operator ==(PlcLibrary lhs, PlcLibrary rhs)
        {
            return lhs?.Name == rhs?.Name && lhs?.Name == rhs?.Name && lhs?.Version == rhs?.Version;
        }
        public static bool operator !=(PlcLibrary lhs, PlcLibrary rhs)
        {
            return !(lhs == rhs);
        }
        public override bool Equals(object o)
        {
            return this == o as PlcLibrary;
        }
        public override int GetHashCode()
        {
            return 17 + Name?.GetHashCode() ?? 0 + 23 * Version?.GetHashCode() ?? 0 + 23 * DistributorName?.GetHashCode() ?? 0;
        }
    }
}
