using System;
using System.Security.Cryptography;

namespace SnowpipeStreaming.Auth;

/// <summary>
/// Utilities for computing RSA public key fingerprints required by Snowflake.
/// </summary>
public static class PublicKeyFingerprint
{
    /// <summary>Prefix used by Snowflake for SHA-256 public key fingerprints.</summary>
    public const string Sha256Prefix = "SHA256:";

    /// <summary>
    /// Computes the fingerprint string in the form "SHA256:&lt;base64&gt;" from an RSA public key.
    /// Uses the SHA-256 hash over the DER-encoded SubjectPublicKeyInfo.
    /// </summary>
    public static string ComputeSha256Fingerprint(RSA rsa)
    {
        if (rsa is null) throw new ArgumentNullException(nameof(rsa));
        var pub = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(pub);
        var b64 = Convert.ToBase64String(hash);
        return $"{Sha256Prefix}{b64}";
    }
}
