using System.Text.Json.Serialization;

namespace SnowpipeStreaming.Models;

/// <summary>
/// Request body for opening a channel.
/// </summary>
public sealed class OpenChannelRequest
{
    /// <summary>
    /// Optional offset token to set when opening a channel.
    /// </summary>
    [JsonPropertyName("offset_token")]
    public string? OffsetToken { get; init; }
}
