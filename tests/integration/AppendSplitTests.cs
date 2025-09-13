using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using SnowpipeStreaming;
using Xunit;

namespace Integration.Tests;

public class AppendSplitTests
{
    [Fact]
    public async Task AppendRows_SplitsPayloadsOver16MB()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ => MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" }));
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch", _ =>
            MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = "cont-1" }));
        int appendCalls = 0;
        handler.Map("POST", "/v2/streaming/data/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch/rows", _ =>
        {
            appendCalls++;
            var token = appendCalls == 1 ? "cont-2" : "cont-3";
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = token });
        });

        var client = new SnowpipeClient(new Uri("http://localhost"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("localhost");
        var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "ch");

        // Create two large rows ~8MB each to force two requests
        int size = 9 * 1024 * 1024; // ensure two rows exceed 16MB combined after JSON serialization
        string big = new string('a', size);
        var rows = new[] { new { data = big }, new { data = big } };

        var next = await channel.AppendRowsAsync(rows);
        next.Should().Be("cont-3");
        appendCalls.Should().Be(2);
    }
}
