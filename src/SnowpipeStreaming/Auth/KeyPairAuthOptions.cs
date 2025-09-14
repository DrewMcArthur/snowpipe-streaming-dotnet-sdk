using System;

namespace SnowpipeStreaming.Auth;

/// <summary>
/// Options for generating Snowflake key-pair JWTs in-application.
/// </summary>
public sealed class KeyPairAuthOptions
{
    /// <summary>Snowflake account identifier (uppercase; exclude region if using account locator).</summary>
    public string AccountIdentifier { get; set; } = string.Empty;
    /// <summary>Snowflake user name (uppercase).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>PKCS#8 PEM private key contents. If not set, <see cref="PrivateKeyPath"/> must be set.</summary>
    public string? PrivateKeyPem { get; set; }
    /// <summary>Filesystem path to a PKCS#8 PEM private key. Optional if <see cref="PrivateKeyPem"/> is provided.</summary>
    public string? PrivateKeyPath { get; set; }
    /// <summary>Passphrase for an encrypted PKCS#8 private key, if applicable.</summary>
    public ReadOnlyMemory<char> Passphrase { get; set; } = ReadOnlyMemory<char>.Empty;

    /// <summary>Optional precomputed SHA-256 public key fingerprint (without the "SHA256:" prefix).</summary>
    public string? PublicKeyFingerprint { get; set; }

    /// <summary>Desired token lifetime. The generator will clamp to a maximum of 1 hour.</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(55);
    /// <summary>Allowed clock skew tolerance when considering token freshness.</summary>
    public TimeSpan ClockSkewTolerance { get; set; } = TimeSpan.FromMinutes(5);
}

