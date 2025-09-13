using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Contract.Tests;
using SnowpipeStreaming;
using Xunit;

public class BulkChannelStatusTests
{
    [Fact]
    public async Task BulkStatus_Post_ReturnsStatuses()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" });
        });
        handler.Map("POST", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE:bulk-channel-status", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new
            {
                channel_statuses = new
                {
                    my_channel = new { channel_status_code = "ACTIVE", last_committed_offset_token = "off-2" }
                }
            });
        });
        var client = new SnowpipeClient(new Uri("https://example.snowflakecomputing.com"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("example.snowflakecomputing.com");
        var ex = await Record.ExceptionAsync(async () =>
            await client.BulkGetChannelStatusAsync("DB", "SCHEMA", "PIPE", new[] { "my_channel" }));
        ex.Should().BeNull(); // will fail until implemented
    }
}
