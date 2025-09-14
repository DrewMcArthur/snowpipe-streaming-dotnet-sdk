using System.Threading;
using System.Threading.Tasks;

namespace SnowpipeStreaming.Auth;

/// <summary>
/// Provides account-host authorization tokens for Snowflake REST calls (e.g., hostname discovery, token exchange).
/// </summary>
public interface IAccountTokenProvider
{
    /// <summary>
    /// Returns the token type for the X-Snowflake-Authorization-Token-Type header (e.g., KEYPAIR_JWT, JWT).
    /// </summary>
    string TokenType { get; }

    /// <summary>
    /// Produces a bearer token to be used in the Authorization header.
    /// </summary>
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

