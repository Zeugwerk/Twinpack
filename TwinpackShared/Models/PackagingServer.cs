using System.Text.Json.Serialization;

namespace Twinpack.Models
{
    public class PackagingServer
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string ServerType { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public bool Connected { get; set; }
    }
}
