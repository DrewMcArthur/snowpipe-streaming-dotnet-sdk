using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using SnowpipeStreaming;
using Xunit;

namespace Contract.Tests;

public class HostnameTests
{
    [Fact]
    public async Task GetHostname_SendsJwtAuthAndTokenTypeHeader_AndReturnsHostname()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("GET", "/v2/streaming/hostname", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { hostname = "acct.host" });
        });

        var client = new SnowpipeClient(new Uri("https://example.snowflakecomputing.com"), "jwt", handler);
        string host = await client.GetHostnameAsync();
        host.Should().Be("acct.host");
        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest!.Headers.Authorization!.Parameter.Should().Be("jwt");
        handler.LastRequest!.Headers.TryGetValues("X-Snowflake-Authorization-Token-Type", out var values).Should().BeTrue();
        values!.First().Should().Be("JWT");
        handler.LastRequest!.Headers.UserAgent.Should().NotBeEmpty();
    }
}
