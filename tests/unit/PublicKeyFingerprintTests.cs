using System.Security.Cryptography;
using FluentAssertions;
using SnowpipeStreaming.Auth;
using Xunit;

public class PublicKeyFingerprintTests
{
    [Fact]
    public void Computes_Fingerprint_From_PublicKey()
    {
        using var rsa = RSA.Create(2048);
        var fp = PublicKeyFingerprint.ComputeSha256Fingerprint(rsa);
        fp.Should().StartWith("SHA256:");
        // Recompute and ensure deterministic
        var fp2 = PublicKeyFingerprint.ComputeSha256Fingerprint(rsa);
        fp2.Should().Be(fp);
    }
}

