using System.Text.Json.Serialization;

namespace SnowpipeStreaming.Models;

/// <summary>
/// Channel status fields returned by Snowpipe Streaming bulk status APIs.
/// </summary>
public sealed class ChannelStatus
{
    /// <summary>Status code for the channel, e.g., ACTIVE.</summary>
    [JsonPropertyName("channel_status_code")]
    public string? ChannelStatusCode { get; init; }
    /// <summary>Latest committed offset token.</summary>
    [JsonPropertyName("last_committed_offset_token")]
    public string? LastCommittedOffsetToken { get; init; }
    /// <summary>Name of the database that the channel belongs to.</summary>
    [JsonPropertyName("database_name")]
    public string? DatabaseName { get; init; }
    /// <summary>Name of the schema that the channel belongs to.</summary>
    [JsonPropertyName("schema_name")]
    public string? SchemaName { get; init; }
    /// <summary>Name of the pipe that the channel belongs to.</summary>
    [JsonPropertyName("pipe_name")]
    public string? PipeName { get; init; }
    /// <summary>Name of the channel.</summary>
    [JsonPropertyName("channel_name")]
    public string? ChannelName { get; init; }
    /// <summary>Total rows inserted into this channel.</summary>
    [JsonPropertyName("rows_inserted")]
    public int? RowsInserted { get; init; }
    /// <summary>Total rows parsed (not necessarily inserted).</summary>
    [JsonPropertyName("rows_parsed")]
    public int? RowsParsed { get; init; }
    /// <summary>Total rows that encountered errors and were rejected.</summary>
    [JsonPropertyName("rows_errors")]
    public int? RowsErrors { get; init; }
    /// <summary>Upper bound token for the offset containing the last error.</summary>
    [JsonPropertyName("last_error_offset_upper_bound")]
    public string? LastErrorOffsetUpperBound { get; init; }
    /// <summary>Human-readable message for the last error (redacted).</summary>
    [JsonPropertyName("last_error_message")]
    public string? LastErrorMessage { get; init; }
    /// <summary>Timestamp (epoch ms) of the last error.</summary>
    [JsonPropertyName("last_error_timestamp")]
    public long? LastErrorTimestamp { get; init; }
    /// <summary>Average end-to-end processing latency in milliseconds.</summary>
    [JsonPropertyName("snowflake_avg_processing_latency_ms")]
    public int? SnowflakeAvgProcessingLatencyMs { get; init; }
}
