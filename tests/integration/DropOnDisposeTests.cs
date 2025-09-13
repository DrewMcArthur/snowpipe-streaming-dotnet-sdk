using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using SnowpipeStreaming;
using Xunit;

namespace Integration.Tests;

public class DropOnDisposeTests
{
    [Fact]
    public async Task Dispose_WaitsForCommit_AndDropsChannel()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ => MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" }));

        // Open returns initial token
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch", _ =>
            MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = "cont-1" }));

        // Bulk status returns committed == cont-1
        int statusCalls = 0;
        handler.Map("POST", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE:bulk-channel-status", _ =>
        {
            statusCalls++;
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new
            {
                channel_statuses = new { ch = new { channel_status_code = "ACTIVE", last_committed_offset_token = "cont-1" } }
            });
        });

        int dropCalls = 0;
        handler.Map("DELETE", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch", _ =>
        {
            dropCalls++;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var client = new SnowpipeClient(new Uri("http://localhost"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("localhost");

        await using (var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "ch", dropOnDispose: true))
        {
            // no-op
        }

        statusCalls.Should().BeGreaterThan(0);
        dropCalls.Should().Be(1);
    }
}
