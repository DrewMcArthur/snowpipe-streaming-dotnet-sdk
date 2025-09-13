using System.Net;

namespace SnowpipeStreaming.Errors;

/// <summary>
/// Base exception for Snowpipe Streaming client errors, carrying HTTP status and Snowflake error fields.
/// </summary>
public class SnowpipeException : Exception
{
    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; }
    /// <summary>
    /// Snowflake error code when provided by the server.
    /// </summary>
    public string? ErrorCode { get; }
    /// <summary>
    /// Request identifier when provided by the server, useful for support.
    /// </summary>
    public string? RequestId { get; }

    /// <summary>
    /// Creates a new <see cref="SnowpipeException"/>.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="errorCode">Snowflake error code, if available.</param>
    /// <param name="requestId">Request identifier, if available.</param>
    /// <param name="inner">Optional inner exception.</param>
    public SnowpipeException(string message, HttpStatusCode statusCode, string? errorCode = null, string? requestId = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        RequestId = requestId;
    }
}

/// <summary>
/// Indicates authentication or authorization failure (e.g., missing or invalid token).
/// </summary>
public sealed class SnowpipeUnauthorizedException : SnowpipeException
{
    /// <summary>
    /// Creates a new <see cref="SnowpipeUnauthorizedException"/>.
    /// </summary>
    public SnowpipeUnauthorizedException(string message, HttpStatusCode statusCode = HttpStatusCode.Unauthorized, string? errorCode = null, string? requestId = null)
        : base(message, statusCode, errorCode, requestId) { }
}

/// <summary>
/// Indicates a bad request (400), typically due to invalid parameters or payload.
/// </summary>
public sealed class SnowpipeBadRequestException : SnowpipeException
{
    /// <summary>
    /// Creates a new <see cref="SnowpipeBadRequestException"/>.
    /// </summary>
    public SnowpipeBadRequestException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, string? errorCode = null, string? requestId = null)
        : base(message, statusCode, errorCode, requestId) { }
}

/// <summary>
/// Indicates a missing resource (404).
/// </summary>
public sealed class SnowpipeNotFoundException : SnowpipeException
{
    /// <summary>
    /// Creates a new <see cref="SnowpipeNotFoundException"/>.
    /// </summary>
    public SnowpipeNotFoundException(string message, HttpStatusCode statusCode = HttpStatusCode.NotFound, string? errorCode = null, string? requestId = null)
        : base(message, statusCode, errorCode, requestId) { }
}

/// <summary>
/// Indicates rate limiting (429) with optional retry delay.
/// </summary>
public sealed class SnowpipeRateLimitException : SnowpipeException
{
    /// <summary>
    /// Suggested delay before retrying, if provided by the server.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
    /// <summary>
    /// Creates a new <see cref="SnowpipeRateLimitException"/>.
    /// </summary>
    public SnowpipeRateLimitException(string message, TimeSpan? retryAfter = null, string? errorCode = null, string? requestId = null)
        : base(message, HttpStatusCode.TooManyRequests, errorCode, requestId)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Indicates a server-side error (5xx).
/// </summary>
public sealed class SnowpipeServerException : SnowpipeException
{
    /// <summary>
    /// Creates a new <see cref="SnowpipeServerException"/>.
    /// </summary>
    public SnowpipeServerException(string message, HttpStatusCode statusCode, string? errorCode = null, string? requestId = null)
        : base(message, statusCode, errorCode, requestId) { }
}
