using System.Text.Json.Serialization;

namespace Twinpack.Protocol.Api
{
    /// <summary>Payload from the notifications endpoint (e.g. latest Twinpack version available for the client).</summary>
    public class TwinpackUpdateNotification : Response
    {
        [JsonPropertyName("latest-version")]
        public string LatestVersion { get; set; }
    }
}
