using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using SnowpipeStreaming;
using Xunit;

namespace Integration.Tests;

public class GetLatestCommittedOffsetTests
{
    [Fact]
    public async Task ReturnsLatestCommittedOffset()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ => MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" }));
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch", _ =>
            MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = "cont-1" }));
        handler.Map("POST", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE:bulk-channel-status", _ =>
            MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { channel_statuses = new { ch = new { last_committed_offset_token = "off-123" } } }));

        var client = new SnowpipeClient(new Uri("http://localhost"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("localhost");
        await using var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "ch");

        var latest = await channel.GetLatestCommittedOffsetTokenAsync();
        latest.Should().Be("off-123");
    }
}

