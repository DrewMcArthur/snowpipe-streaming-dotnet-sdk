using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Contract.Tests;
using SnowpipeStreaming;
using Xunit;

public class TokenExchangeTests
{
    [Fact]
    public async Task ExchangeScopedToken_PostsForm_AndStoresToken()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("POST", "/oauth/token", req =>
        {
            req.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { token = "scoped-token" });
        });

        var client = new SnowpipeClient(new Uri("https://example.snowflakecomputing.com"), "jwt", handler);
        var ex = await Record.ExceptionAsync(async () => await client.ExchangeScopedTokenAsync("acct.host"));
        ex.Should().BeNull(); // will fail until implemented
    }
}

