using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Contract.Tests;
using SnowpipeStreaming;
using Xunit;
using System.Net.Http;

public class DropChannelTests
{
    [Fact]
    public async Task DropChannel_Delete_NoContent()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", _ =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"token\":\"scoped-token\"}", System.Text.Encoding.UTF8, "application/json")
            };
        });
        handler.Map("PUT", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/my_channel", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { next_continuation_token = "cont-ignored" });
        });
        handler.Map("DELETE", "/v2/streaming/databases/DB/schemas/SCHEMA/pipes/PIPE/channels/my_channel", _ =>
        {
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = new SnowpipeClient(new Uri("https://example.snowflakecomputing.com"), "jwt", handler);
        await client.ExchangeScopedTokenAsync("example.snowflakecomputing.com");
        var channel = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "my_channel");
        var ex = await Record.ExceptionAsync(async () => await channel.DropAsync());
        ex.Should().BeNull(); // will fail until implemented

        // After drop, using the channel should fail
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await channel.GetLatestCommittedOffsetTokenAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await channel.AppendRowsAsync(new[] { "{}" }));
    }
}
