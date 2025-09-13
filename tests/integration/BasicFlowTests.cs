using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using SnowpipeStreaming;
using Xunit;

namespace Integration.Tests;

public class BasicFlowTests
{
    [Fact]
    public async Task Basic_EndToEnd_Flow_Succeeds()
    {
        var handler = new MockSnowflakeServerHandler();
        string lastContinuation = "cont-1";
        string lastCommittedOffset = "off-1";

        // Account host (e.g., management plane)
        var accountHost = "localhost:5001";
        // Ingest host (returned by hostname endpoint)
        var ingestHost = "localhost:5002";

        handler.MapHost(accountHost, "GET", "/v2/streaming/hostname", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { hostname = ingestHost });
        });
        handler.MapHost(accountHost, "POST", "/oauth/token", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" });
        });
        // Ingest endpoints on separate host
        handler.MapHost(ingestHost, "PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/my_channel", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new
            {
                next_continuation_token = lastContinuation,
                channel_status = new { channel_name = "my_channel", channel_status_code = "ACTIVE", last_committed_offset_token = "off-1" }
            });
        });
        handler.MapHost(ingestHost, "POST", "/v2/streaming/data/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/my_channel/rows", _ =>
        {
            lastContinuation = "cont-2";
            // Simulate server commit progression after append
            lastCommittedOffset = lastContinuation;
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { nextContinuationToken = lastContinuation });
        });
        handler.MapHost(ingestHost, "POST", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE:bulk-channel-status", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new
            {
                channel_statuses = new
                {
                    my_channel = new { channel_status_code = "ACTIVE", last_committed_offset_token = lastCommittedOffset }
                }
            });
        });

        var client = new SnowpipeClient(new Uri($"http://{accountHost}"), "jwt", handler);

        var ex = await Record.ExceptionAsync(async () =>
        {
            var hostname = await client.GetHostnameAsync(); // Should call accountHost
            hostname.Should().NotBeNullOrEmpty();
            // Simulate docs behavior: client receives a hostname and uses it for ingest endpoints
            await client.ExchangeScopedTokenAsync(hostname);
            var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "my_channel");
            var next = await channel.AppendRowsAsync(new[] { "{}" });
            await channel.WaitForCommitAsync(next);
        });
        ex.Should().BeNull(); // will fail until implemented
    }
}
