using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Contract.Tests;

public class UserAgentTests
{
    [Fact]
    public async Task MissingUserAgent_ReturnsBadRequest()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("GET", "/v2/streaming/hostname", _ =>
        {
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { hostname = "ignored" });
        });
        using var http = new HttpClient(handler, disposeHandler: true);
        // Intentionally do not set User-Agent
        var resp = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost/v2/streaming/hostname"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}

