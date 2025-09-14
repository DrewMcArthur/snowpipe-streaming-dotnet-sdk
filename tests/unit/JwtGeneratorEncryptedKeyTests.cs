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
        var pem = TestPem.ToPem("ENCRYPTED PRIVATE KEY", encrypted);

        var optsWrong = new KeyPairAuthOptions
        {
            AccountIdentifier = "ACC",
            UserName = "USER",
            PrivateKeyPem = pem,
        };
        Action actWrong = () => JwtGenerator.GenerateJwt(optsWrong);
        var ex = actWrong.Should().Throw<Exception>().Which;
        (ex is CryptographicException || ex is ArgumentException).Should().BeTrue();

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

    
}
