using System;
using System.Security.Cryptography;
using FluentAssertions;
using SnowpipeStreaming.Auth;
using Xunit;

public class JwtGeneratorEncryptedKeyTests
{
    [Fact]
    public void Encrypted_Key_Requires_Passphrase()
    {
        using var rsa = RSA.Create(2048);
        var encrypted = rsa.ExportEncryptedPkcs8PrivateKey("pass123".AsSpan(), new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
        var pem = ToPem("ENCRYPTED PRIVATE KEY", encrypted);

        var optsWrong = new KeyPairAuthOptions
        {
            AccountIdentifier = "ACC",
            UserName = "USER",
            PrivateKeyPem = pem,
        };
        Action actWrong = () => JwtGenerator.GenerateJwt(optsWrong);
        actWrong.Should().Throw<CryptographicException>();

        var optsRight = new KeyPairAuthOptions
        {
            AccountIdentifier = "ACC",
            UserName = "USER",
            PrivateKeyPem = pem,
            Passphrase = "pass123".AsMemory()
        };
        var jwt = JwtGenerator.GenerateJwt(optsRight);
        jwt.Should().NotBeNullOrEmpty();
    }

    private static string ToPem(string type, byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"-----BEGIN {type}-----");
        for (int i = 0; i < b64.Length; i += 64)
        {
            sb.AppendLine(b64.Substring(i, Math.Min(64, b64.Length - i)));
        }
        sb.AppendLine($"-----END {type}-----");
        return sb.ToString();
    }
}

