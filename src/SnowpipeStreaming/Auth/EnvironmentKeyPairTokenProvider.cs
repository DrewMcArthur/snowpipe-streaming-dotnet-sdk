using System;
using System.Threading;
using System.Threading.Tasks;

namespace SnowpipeStreaming.Auth;

/// <summary>
/// Builds a KeyPairJwtTokenProvider from environment variables.
/// Variables: SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PRIVATE_KEY or SNOWFLAKE_PRIVATE_KEY_PATH, SNOWFLAKE_PRIVATE_KEY_PASSPHRASE.
/// </summary>
public sealed class EnvironmentKeyPairTokenProvider : IAccountTokenProvider
{
    private readonly KeyPairJwtTokenProvider _inner;

    /// <inheritdoc />
    public string TokenType => _inner.TokenType;

    /// <summary>
    /// Initializes the provider by reading environment variables for Snowflake account, user, and private key material.
    /// </summary>
    public EnvironmentKeyPairTokenProvider()
    {
        var account = Environment.GetEnvironmentVariable("SNOWFLAKE_ACCOUNT") ?? string.Empty;
        var user = Environment.GetEnvironmentVariable("SNOWFLAKE_USER") ?? string.Empty;
        var pem = Environment.GetEnvironmentVariable("SNOWFLAKE_PRIVATE_KEY");
        var path = Environment.GetEnvironmentVariable("SNOWFLAKE_PRIVATE_KEY_PATH");
        var pass = Environment.GetEnvironmentVariable("SNOWFLAKE_PRIVATE_KEY_PASSPHRASE");
        var opts = new KeyPairAuthOptions
        {
            AccountIdentifier = account,
            UserName = user,
            PrivateKeyPem = string.IsNullOrWhiteSpace(pem) ? null : pem,
            PrivateKeyPath = string.IsNullOrWhiteSpace(path) ? null : path,
            Passphrase = string.IsNullOrEmpty(pass) ? ReadOnlyMemory<char>.Empty : pass.AsMemory()
        };
        _inner = new KeyPairJwtTokenProvider(opts);
    }

    /// <inheritdoc />
    public Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
        => _inner.GetTokenAsync(cancellationToken);
}
