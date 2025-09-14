using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Contract.Tests;
using FluentAssertions;
using SnowpipeStreaming;
using SnowpipeStreaming.Auth;
using Xunit;

public class KeyPairAuthHeaderTests
{
    [Fact]
    public async Task Hostname_Uses_KeyPairJwt_Headers()
    {
        var handler = new MockSnowflakeServerHandler();
        handler.Map("GET", "/v2/streaming/hostname", req =>
        {
            req.Headers.Authorization.Should().NotBeNull();
            req.Headers.Authorization!.Scheme.Should().Be("Bearer");
            req.Headers.TryGetValues("X-Snowflake-Authorization-Token-Type", out var vals).Should().BeTrue();
            vals!.Should().Contain("KEYPAIR_JWT");
            return MockSnowflakeServerHandler.Json(HttpStatusCode.OK, new { hostname = "localhost" });
        });

        // Generate an ephemeral key for the test and feed it via PEM
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        var pem = TestPem.ToPem("PRIVATE KEY", pkcs8);

        var opts = new KeyPairAuthOptions
        {
            AccountIdentifier = "xy12345.us-east-1",
            UserName = "user1",
            PrivateKeyPem = pem,
            TokenLifetime = TimeSpan.FromMinutes(5)
        };
        var provider = new KeyPairJwtTokenProvider(opts);
        var client = new SnowpipeClient(new Uri("http://localhost"), provider, handler);

        var hostname = await client.GetHostnameAsync();
        hostname.Should().Be("localhost");
    }


}
