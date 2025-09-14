using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SnowpipeStreaming.Auth;

/// <summary>
/// Generates Snowflake-compatible JWTs for key-pair authentication.
/// </summary>
public static class JwtGenerator
{
    /// <summary>
    /// Generates a JWT signed with RS256 using the provided options. Computes the SHA256 public key fingerprint
    /// if not provided and constructs claims per Snowflake guidance: iss, sub, iat, exp.
    /// </summary>
    public static string GenerateJwt(KeyPairAuthOptions options, DateTimeOffset? now = null)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        string account = NormalizeAccountIdentifier(options.AccountIdentifier);
        string user = NormalizeUser(options.UserName);
        if (string.IsNullOrWhiteSpace(account)) throw new ArgumentException("AccountIdentifier required", nameof(options.AccountIdentifier));
        if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("UserName required", nameof(options.UserName));

        using var rsa = LoadRsaFromOptions(options);
        string fp = options.PublicKeyFingerprint ?? PublicKeyFingerprint.ComputeSha256Fingerprint(rsa).Substring("SHA256:".Length);

        var issued = (now ?? DateTimeOffset.UtcNow);
        var lifetime = options.TokenLifetime <= TimeSpan.Zero ? TimeSpan.FromMinutes(55) : options.TokenLifetime;
        if (lifetime > TimeSpan.FromHours(1)) lifetime = TimeSpan.FromHours(1);
        var exp = issued.Add(lifetime);

        var header = new { alg = "RS256", typ = "JWT" };
        var payload = new
        {
            iss = $"{account}.{user}.SHA256:{fp}",
            sub = $"{account}.{user}",
            iat = issued.ToUnixTimeSeconds(),
            exp = exp.ToUnixTimeSeconds()
        };

        string headerB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        string payloadB64 = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");

        byte[] signature = rsa.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string sigB64 = Base64UrlEncode(signature);
        return $"{headerB64}.{payloadB64}.{sigB64}";
    }

    /// <summary>Uppercases and removes region suffix from account locator if present (segment after first dot).</summary>
    public static string NormalizeAccountIdentifier(string accountIdentifier)
    {
        if (string.IsNullOrWhiteSpace(accountIdentifier)) return string.Empty;
        var core = accountIdentifier.Split('.', 2)[0];
        return core.ToUpperInvariant();
    }

    /// <summary>Uppercases the user name.</summary>
    public static string NormalizeUser(string user) => string.IsNullOrWhiteSpace(user) ? string.Empty : user.ToUpperInvariant();

    private static RSA LoadRsaFromOptions(KeyPairAuthOptions options)
    {
        string pem = options.PrivateKeyPem ?? (options.PrivateKeyPath is null ? null : System.IO.File.ReadAllText(options.PrivateKeyPath))
            ?? throw new ArgumentException("PrivateKeyPem or PrivateKeyPath must be provided", nameof(options));
        var rsa = RSA.Create();
        ReadOnlySpan<char> pemSpan = pem.AsSpan();
        if (!options.Passphrase.IsEmpty)
        {
            rsa.ImportFromEncryptedPem(pemSpan, options.Passphrase.Span);
        }
        else
        {
            rsa.ImportFromPem(pemSpan);
        }
        return rsa;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        string b64 = Convert.ToBase64String(data);
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

