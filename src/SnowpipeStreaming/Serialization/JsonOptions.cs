using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnowpipeStreaming.Serialization;

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = Create();

    private static JsonSerializerOptions Create()
    {
        var opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };
        return opts;
    }
}

