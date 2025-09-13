using System.Text.Json.Serialization;

namespace SnowpipeStreaming.Models;

/// <summary>
/// Response payload for opening a channel, including the next continuation token and current channel status.
/// </summary>
public sealed class OpenChannelResponse
{
    /// <summary>
    /// Continuation token to be used for the next Append Rows request.
    /// </summary>
    [JsonPropertyName("next_continuation_token")]
    public string? NextContinuationToken { get; init; }

    /// <summary>
    /// Current status details for the channel.
    /// </summary>
    [JsonPropertyName("channel_status")]
    public ChannelStatus? ChannelStatus { get; init; }
}
