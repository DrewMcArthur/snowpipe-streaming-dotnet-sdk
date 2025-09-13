using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Contract.Tests;
using SnowpipeStreaming;
using Xunit;
using System.Linq;

public class OpenChannelTests
{
    [Fact]
    public async Task OpenChannel_Put_ReturnsContinuationTokenAndStatus()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" });
        });
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/my_channel", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new
            {
                next_continuation_token = "cont-1",
                channel_status = new { channel_name = "my_channel", channel_status_code = "ACTIVE" }
            });
        });
        var client = new SnowpipeClient(new Uri("https://example.snowflakecomputing.com"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("example.snowflakecomputing.com");
        var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "my_channel");
        channel.LatestContinuationToken.Should().Be("cont-1");
        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest!.Headers.Authorization!.Parameter.Should().Be("scoped-token");
        handler.LastRequest!.Headers.TryGetValues("X-Snowflake-Authorization-Token-Type", out var vals).Should().BeTrue();
        vals!.First().Should().Be("OAuth");
    }
}
