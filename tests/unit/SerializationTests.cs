using System.Text.Json;
using FluentAssertions;
using SnowpipeStreaming.Models;
using Xunit;

namespace Unit.Tests;

public class SerializationTests
{
    [Fact]
    public void ChannelStatus_Deserializes_FromSnakeCase()
    {
        var json = """
{
  "channel_status_code": "ACTIVE",
  "last_committed_offset_token": "off-2",
  "database_name": "DB",
  "schema_name": "SCHEMA",
  "pipe_name": "PIPE",
  "channel_name": "ch",
  "rows_inserted": 10,
  "rows_parsed": 12,
  "rows_errors": 2,
  "last_error_offset_upper_bound": "off-1",
  "last_error_message": "err",
  "last_error_timestamp": 123,
  "snowflake_avg_processing_latency_ms": 50
}
""";
        var cs = JsonSerializer.Deserialize<ChannelStatus>(json);
        cs!.ChannelStatusCode.Should().Be("ACTIVE");
        cs.LastCommittedOffsetToken.Should().Be("off-2");
        cs.DatabaseName.Should().Be("DB");
        cs.SchemaName.Should().Be("SCHEMA");
        cs.PipeName.Should().Be("PIPE");
        cs.ChannelName.Should().Be("ch");
        cs.RowsInserted.Should().Be(10);
        cs.RowsParsed.Should().Be(12);
        cs.RowsErrors.Should().Be(2);
        cs.LastErrorOffsetUpperBound.Should().Be("off-1");
    }

    [Fact]
    public void OpenChannelResponse_Deserializes_WithNestedStatus()
    {
        var json = """
{
  "next_continuation_token": "cont-1",
  "channel_status": { "channel_status_code": "ACTIVE" }
}
""";
        var resp = JsonSerializer.Deserialize<OpenChannelResponse>(json);
        resp!.NextContinuationToken.Should().Be("cont-1");
        resp.ChannelStatus!.ChannelStatusCode.Should().Be("ACTIVE");
    }

    [Fact]
    public void OpenChannelRequest_Serializes_OffsetToken_SnakeCase()
    {
        var req = new OpenChannelRequest { OffsetToken = "off-1" };
        var json = JsonSerializer.Serialize(req);
        json.Should().Contain("offset_token");
        json.Should().Contain("off-1");
    }
}

