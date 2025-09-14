using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SnowpipeStreaming.Auth;

/// <summary>
/// Token provider that generates RS256 JWTs for Snowflake key-pair authentication.
/// Caches the token until near expiry and regenerates as needed.
/// </summary>
public sealed class KeyPairJwtTokenProvider : IAccountTokenProvider
{
    private readonly KeyPairAuthOptions _options;
    private readonly object _lock = new();
    private string? _cached;
    private DateTimeOffset _expiresAt;

    /// <inheritdoc />
    public string TokenType => "KEYPAIR_JWT";

    /// <summary>
    /// Creates a provider that generates and caches KEYPAIR_JWT tokens using the provided options.
    /// </summary>
    /// <param name="options">Key pair authentication options (account, user, private key, optional passphrase, lifetime).</param>
    public KeyPairJwtTokenProvider(KeyPairAuthOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(_cached) && now.Add(_options.ClockSkewTolerance) < _expiresAt)
            {
                return Task.FromResult(_cached);
            }
            var jwt = JwtGenerator.GenerateJwt(_options, now);
            // Parse exp to compute freshness window
            var parts = jwt.Split('.');
            if (parts.Length != 3) throw new InvalidOperationException("Generated JWT is invalid");
            var payloadJson = System.Text.Encoding.UTF8.GetString(SnowpipeStreaming.Util.Base64Url.Decode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            var exp = doc.RootElement.GetProperty("exp").GetInt64();
            _expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
            _cached = jwt;
            return Task.FromResult(jwt);
        }
    }

    
}
