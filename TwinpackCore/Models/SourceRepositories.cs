using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Twinpack.Models
{
    public class SourceRepositories
    {
        [JsonPropertyName("repositories")]
        public List<PackagingServer> PackagingServers { get; set; } = new List<PackagingServer>();
    }
}
