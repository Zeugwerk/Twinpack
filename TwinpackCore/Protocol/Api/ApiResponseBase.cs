using System.Text.Json.Serialization;

namespace Twinpack.Protocol.Api
{
    public class JwtPayload
    {
        [JsonPropertyName("iss")]
        public string Issuer { get; set; }
        [JsonPropertyName("exp")]
        public int ExpirationTime { get; set; }
    }

    public class PaginationHeader
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }
        [JsonPropertyName("self")]
        public string Self { get; set; }
        [JsonPropertyName("next")]
        public string Next { get; set; }
        [JsonPropertyName("prev")]
        public string Prev { get; set; }
    }

    public class Response
    {
        public class ResponseMeta
        {
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }

        [JsonPropertyName("_meta")]
        public ResponseMeta Meta { get; set; }
    }
}
