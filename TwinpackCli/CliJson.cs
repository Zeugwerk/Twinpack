using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Twinpack.Commands
{
    static class CliJson
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static void Write(object value)
        {
            Console.WriteLine(JsonSerializer.Serialize(value, Options));
        }
    }
}
