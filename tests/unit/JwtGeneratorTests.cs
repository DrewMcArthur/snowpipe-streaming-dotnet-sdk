using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using SnowpipeStreaming.Auth;
using Xunit;

public class JwtGeneratorTests
{
    [Fact]
    public void Generates_Claims_And_Signature_RS256()
    {
        using var rsa = RSA.Create(2048);
        var pem = TestPem.ToPem("PRIVATE KEY", rsa.ExportPkcs8PrivateKey());
        var opts = new KeyPairAuthOptions
        {
            AccountIdentifier = "myorg-myacct",
            UserName = "myuser",
            PrivateKeyPem = pem,
            TokenLifetime = TimeSpan.FromMinutes(10)
        };
        var now = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        string jwt = JwtGenerator.GenerateJwt(opts, now);
        var parts = jwt.Split('.');
        parts.Length.Should().Be(3);

        var payloadJson = System.Text.Encoding.UTF8.GetString(FromBase64Url(parts[1]));
        using var doc = JsonDocument.Parse(payloadJson);
        var iss = doc.RootElement.GetProperty("iss").GetString();
        var sub = doc.RootElement.GetProperty("sub").GetString();
        iss.Should().StartWith("MYORG-MYACCT.MYUSER.SHA256:");
        sub.Should().Be("MYORG-MYACCT.MYUSER");
        var iat = doc.RootElement.GetProperty("iat").GetInt64();
        var exp = doc.RootElement.GetProperty("exp").GetInt64();
        (exp - iat).Should().Be(600);

        // Verify signature
        var signed = System.Text.Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        var sig = FromBase64Url(parts[2]);
        rsa.VerifyData(signed, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().BeTrue();
    }

    [Fact]
    public void Normalizes_Account_And_User()
    {
        using var rsa = RSA.Create(2048);
        var pem = TestPem.ToPem("PRIVATE KEY", rsa.ExportPkcs8PrivateKey());
        var opts = new KeyPairAuthOptions
        {
            AccountIdentifier = "xy12345.us-east-1",
            UserName = "user1",
            PrivateKeyPem = pem,
        };
        string jwt = JwtGenerator.GenerateJwt(opts);
        var payloadJson = System.Text.Encoding.UTF8.GetString(FromBase64Url(jwt.Split('.')[1]));
        using var doc = JsonDocument.Parse(payloadJson);
        doc.RootElement.GetProperty("sub").GetString().Should().Be("XY12345.USER1");
        doc.RootElement.GetProperty("iss").GetString().Should().StartWith("XY12345.USER1.SHA256:");
    }

    

    private static byte[] FromBase64Url(string s)
    {
        string padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
