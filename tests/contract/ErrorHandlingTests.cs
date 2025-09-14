using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Contract.Tests;
using FluentAssertions;
using SnowpipeStreaming;
using SnowpipeStreaming.Errors;
using Xunit;

public class ErrorHandlingTests
{
    [Fact]
    public async Task MissingJwtOnHostname_ThrowsUnauthorized()
    {
        var handler = new MockSnowflakeServerHandler();
        var client = new SnowpipeClient(new Uri("http://localhost"), string.Empty, handler);
        await Assert.ThrowsAsync<SnowpipeStreaming.Errors.SnowpipeUnauthorizedException>(async () =>
            await client.GetHostnameAsync());
    }

    [Fact]
    public async Task IngestCall_WithoutScopedToken_FailsFastWithPrecondition()
    {
        var handler = new MockSnowflakeServerHandler();
        // No oauth mapping; client should fail fast before network call
        var client = new SnowpipeClient(new Uri("http://localhost"), "jwt", handler);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "ch"));
        ex.Message.Should().Contain("Ingest hostname not set");
    }

    [Fact]
    public async Task ApiErrorPayload_MapsToBadRequestException()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ => MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "t" }));
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.BadRequest, new { error_code = "INVALID", message = "invalid request", requestId = "req-123" });
        });
        var client = new SnowpipeClient(new Uri("http://localhost"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("localhost");

        var ex = await Assert.ThrowsAsync<SnowpipeBadRequestException>(async () =>
            await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "ch"));
        ex.ErrorCode.Should().Be("INVALID");
        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.RequestId.Should().Be("req-123");
        ex.Message.Should().Contain("req-123");
    }

    [Fact]
    public async Task InvalidScopedToken_ThrowsUnauthorized()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ => MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "correct-token" }));
        // Any ingest route will do; it won't be reached due to auth enforcement
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = "cont" });
        });
        handler.Map("DELETE", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/ch", _ => new HttpResponseMessage(HttpStatusCode.NoContent));

        var client = new SnowpipeClient(new Uri("http://localhost"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("localhost");

        // Open a channel successfully using the valid scoped token
        var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "ch");

        // Corrupt the token using reflection to simulate mismatch for subsequent calls
        var tokField = typeof(SnowpipeClient).GetField("_scopedToken", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        tokField!.SetValue(client, "wrong-token");
        await Assert.ThrowsAsync<SnowpipeUnauthorizedException>(async () =>
            await channel.DropAsync());
    }
}
