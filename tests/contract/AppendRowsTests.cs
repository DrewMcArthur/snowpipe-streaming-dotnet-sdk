using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Contract.Tests;
using SnowpipeStreaming;
using Xunit;

public class AppendRowsTests
{
    [Fact]
    public async Task AppendRows_PostNdjson_ReturnsNextContinuation()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" });
        });
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/my_channel", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = "cont-1" });
        });
        handler.Map("POST", "/v2/streaming/data/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/my_channel/rows", req =>
        {
            var query = MockSnowflakeServerHandler.ParseQuery(req.RequestUri!);
            query.Should().ContainKey("continuationToken");
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = "cont-2" });
        });
        var client = new SnowpipeClient(new Uri("https://example.snowflakecomputing.com"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("example.snowflakecomputing.com");
        var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "my_channel");
        var ex = await Record.ExceptionAsync(async () =>
            await channel.AppendRowsAsync(new[]{"{}"}));
        ex.Should().BeNull();
    }
}
