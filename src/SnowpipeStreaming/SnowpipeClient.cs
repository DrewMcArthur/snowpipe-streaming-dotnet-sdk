using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using SnowpipeStreaming.Models;
using SnowpipeStreaming.Errors;
using System.Net;

namespace SnowpipeStreaming;

/// <summary>
/// A lightweight, single-responsibility client for the Snowpipe Streaming REST API.
/// It uses the account URL for control-plane endpoints and the discovered ingest host for data-plane endpoints.
/// </summary>
public sealed class SnowpipeClient : IDisposable, IAsyncDisposable
{
    private readonly Uri _accountUrl;
    private readonly string _jwt;
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly JsonSerializerOptions _json;
    private readonly ILogger? _logger;

    private string? _scopedToken;
    private Uri? _ingestBaseUri;

    // Expose logger to internal collaborators (e.g., SnowpipeChannel) without leaking publicly.
    internal ILogger? Logger => _logger;

    /// <summary>
    /// Creates a Snowpipe Streaming client bound to an account URL and caller-provided JWT.
    /// Uses the account host for control-plane calls and the discovered ingest host for data-plane calls.
    /// </summary>
    /// <param name="accountUrl">Base account URL (e.g., https://{account}.{region}.snowflakecomputing.com or http://localhost:port for tests).</param>
    /// <param name="jwt">Caller-provided JWT used to exchange for a scoped token.</param>
    /// <param name="handler">Optional HTTP message handler for testing.</param>
    /// <param name="logger">Optional logger for trace/debug output.</param>
    public SnowpipeClient(Uri accountUrl, string jwt, HttpMessageHandler? handler = null, ILogger? logger = null)
    {
        _accountUrl = accountUrl ?? throw new ArgumentNullException(nameof(accountUrl));
        _jwt = jwt ?? throw new ArgumentNullException(nameof(jwt));
        if (handler is null)
        {
            _http = new HttpClient();
            _ownsHttp = true;
        }
        else
        {
            _http = new HttpClient(handler, disposeHandler: false);
            _ownsHttp = false;
        }
        _http.Timeout = TimeSpan.FromSeconds(100);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SnowpipeStreaming.NET/0.1.0");
        _json = Serialization.JsonOptions.Default;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the ingest hostname for the current account.
    /// </summary>
    /// <remarks>
    /// Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#get-hostname
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ingest hostname (host[:port]).</returns>
    public async Task<string> GetHostnameAsync(CancellationToken cancellationToken = default)
    {
        var uri = Combine(_accountUrl, "/v2/streaming/hostname");
        var (resp, body) = await SendWithRetryAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwt);
            req.Headers.TryAddWithoutValidation("X-Snowflake-Authorization-Token-Type", "JWT");
            return req;
        }, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(resp, body);
        using var doc = JsonDocument.Parse(body);
        var hostname = doc.RootElement.GetProperty("hostname").GetString() ?? throw new InvalidOperationException("hostname missing");
        return hostname;
    }

    /// <summary>
    /// Exchanges the caller JWT for a Snowpipe Streaming scoped token and sets the ingest base URI.
    /// </summary>
    /// <remarks>
    /// Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#exchange-scoped-token
    /// </remarks>
    /// <param name="hostname">The ingest hostname returned by <see cref="GetHostnameAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExchangeScopedTokenAsync(string hostname, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostname)) throw new ArgumentException("Hostname required", nameof(hostname));
        // Set ingest base using same scheme as account URL
        _ingestBaseUri = new Uri($"{_accountUrl.Scheme}://{hostname}");

        var uri = Combine(_accountUrl, "/oauth/token");
        var (resp, body) = await SendWithRetryAsync(() =>
        {
            var content = new StringContent("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer&scope=" + Uri.EscapeDataString(hostname), Encoding.UTF8, "application/x-www-form-urlencoded");
            var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwt);
            req.Headers.TryAddWithoutValidation("X-Snowflake-Authorization-Token-Type", "JWT");
            return req;
        }, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(resp, body);
        using var doc = JsonDocument.Parse(body);
        _scopedToken = doc.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("token missing");
    }

    /// <summary>
    /// Creates or opens a streaming channel for the specified pipe.
    /// </summary>
    /// <remarks>
    /// Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#open-channel
    /// </remarks>
    /// <param name="dropOnDispose">When true, disposing the returned channel waits for commit and then drops the channel server-side.</param>
    /// <param name="database">Database name.</param>
    /// <param name="schema">Schema name.</param>
    /// <param name="pipe">Pipe name.</param>
    /// <param name="channelName">Channel name to create or open.</param>
    /// <param name="offsetToken">Optional offset token to set upon open.</param>
    /// <param name="requestId">Optional request identifier (UUID) for tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SnowpipeChannel"/> bound to the provided identifiers with the initial continuation token set.</returns>
    public async Task<SnowpipeChannel> OpenChannelAsync(
        string database,
        string schema,
        string pipe,
        string channelName,
        string? offsetToken = null,
        Guid? requestId = null,
        bool dropOnDispose = false,
        CancellationToken cancellationToken = default)
    {
        EnsureIngestReady();
        var path = $"/v2/streaming/databases/{Uri.EscapeDataString(database)}/schemas/{Uri.EscapeDataString(schema)}/pipes/{Uri.EscapeDataString(pipe)}/channels/{Uri.EscapeDataString(channelName)}";
        var uri = Combine(_ingestBaseUri!, path, requestId is null ? null : ($"requestId={Uri.EscapeDataString(requestId.Value.ToString())}"));

        var (resp, body) = await SendWithRetryAsync(() =>
        {
            HttpContent? content = null;
            if (!string.IsNullOrEmpty(offsetToken))
            {
                var payload = JsonSerializer.Serialize(new OpenChannelRequest { OffsetToken = offsetToken }, _json);
                content = new StringContent(payload, Encoding.UTF8, "application/json");
            }
            var req = new HttpRequestMessage(HttpMethod.Put, uri) { Content = content };
            AddAuth(req);
            return req;
        }, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(resp, body);
        var result = JsonSerializer.Deserialize<OpenChannelResponse>(body, _json) ?? throw new InvalidOperationException("Invalid response");
        return new SnowpipeChannel(this, database, schema, pipe, channelName, result.NextContinuationToken, dropOnDispose: dropOnDispose);
    }

    /// <summary>
    /// Appends NDJSON rows to a channel using the provided continuation token.
    /// </summary>
    /// <remarks>
    /// Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#append-rows
    /// </remarks>
    /// <param name="database">Database name.</param>
    /// <param name="schema">Schema name.</param>
    /// <param name="pipe">Pipe name.</param>
    /// <param name="channelName">Channel name.</param>
    /// <param name="continuationToken">Continuation token from open/previous append.</param>
    /// <param name="ndjsonLines">Pre-serialized NDJSON lines (one JSON per line).</param>
    /// <param name="offsetToken">Optional offset token to associate with the batch.</param>
    /// <param name="requestId">Optional request identifier (UUID) for tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next continuation token to be used for the subsequent append.</returns>
    public async Task<string> AppendRowsAsync(
        string database,
        string schema,
        string pipe,
        string channelName,
        string continuationToken,
        IEnumerable<string> ndjsonLines,
        string? offsetToken = null,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureIngestReady();
        if (string.IsNullOrWhiteSpace(continuationToken)) throw new ArgumentException("continuationToken required", nameof(continuationToken));
        // Batch pre-serialized lines into <=16MB chunks and send sequentially.
        const int MaxBytes = 16 * 1024 * 1024;
        var sb = new StringBuilder();
        int currentBytes = 0;
        string currentToken = continuationToken;

        static int ByteLen(string s) => Encoding.UTF8.GetByteCount(s);

        foreach (var line in ndjsonLines ?? Array.Empty<string>())
        {
            var withNl = line.EndsWith("\n") ? line : line + "\n";
            int len = ByteLen(withNl);
            if (len > MaxBytes)
            {
                throw new ArgumentException("A single row exceeds the 16MB request size limit after serialization.");
            }
            if (currentBytes > 0 && currentBytes + len > MaxBytes)
            {
                currentToken = await SendAppendChunk(database, schema, pipe, channelName, currentToken, sb.ToString(), offsetToken, requestId, cancellationToken).ConfigureAwait(false);
                sb.Clear();
                currentBytes = 0;
                // offsetToken should only apply to the first batch per API intent
                offsetToken = null;
            }
            sb.Append(withNl);
            currentBytes += len;
        }

        if (currentBytes > 0)
        {
            currentToken = await SendAppendChunk(database, schema, pipe, channelName, currentToken, sb.ToString(), offsetToken, requestId, cancellationToken).ConfigureAwait(false);
        }
        return currentToken;
    }

    /// <summary>
    /// Appends strongly-typed rows, serialized to NDJSON, splitting requests so each payload is under 16MB.
    /// </summary>
    /// <param name="database">Database name.</param>
    /// <param name="schema">Schema name.</param>
    /// <param name="pipe">Pipe name.</param>
    /// <param name="channelName">Channel name.</param>
    /// <param name="continuationToken">Continuation token from open/previous append.</param>
    /// <param name="rows">Rows to serialize and append.</param>
    /// <param name="offsetToken">Optional offset token to associate with the batch.</param>
    /// <param name="requestId">Optional request identifier (UUID) for tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> AppendRowsAsync<T>(
        string database,
        string schema,
        string pipe,
        string channelName,
        string continuationToken,
        IEnumerable<T> rows,
        string? offsetToken = null,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var options = _json;
        IEnumerable<string> Lines()
        {
            foreach (var row in rows ?? Array.Empty<T>())
            {
                yield return JsonSerializer.Serialize(row!, options);
            }
        }
        return await AppendRowsAsync(database, schema, pipe, channelName, continuationToken, Lines(), offsetToken, requestId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendAppendChunk(
        string database,
        string schema,
        string pipe,
        string channelName,
        string continuationToken,
        string ndjson,
        string? offsetToken,
        Guid? requestId,
        CancellationToken cancellationToken)
    {
        var basePath = $"/v2/streaming/data/databases/{Uri.EscapeDataString(database)}/schemas/{Uri.EscapeDataString(schema)}/pipes/{Uri.EscapeDataString(pipe)}/channels/{Uri.EscapeDataString(channelName)}/rows";
        var queries = new List<string> { $"continuationToken={Uri.EscapeDataString(continuationToken)}" };
        if (!string.IsNullOrEmpty(offsetToken)) queries.Add($"offsetToken={Uri.EscapeDataString(offsetToken)}");
        if (requestId is not null) queries.Add($"requestId={Uri.EscapeDataString(requestId.Value.ToString())}");
        var uri = Combine(_ingestBaseUri!, basePath, string.Join("&", queries));

        var (resp, body) = await SendWithRetryAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(ndjson.EndsWith("\n") ? ndjson : ndjson + "\n", Encoding.UTF8, "application/x-ndjson")
            };
            AddAuth(req);
            return req;
        }, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(resp, body);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("next_continuation_token", out var nct))
            return nct.GetString() ?? throw new InvalidOperationException("next_continuation_token missing");
        return doc.RootElement.GetProperty("nextContinuationToken").GetString() ?? throw new InvalidOperationException("nextContinuationToken missing");
    }

    /// <summary>
    /// Retrieves channel status for multiple channels in a single request.
    /// </summary>
    /// <remarks>
    /// Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#bulk-get-channel-status
    /// </remarks>
    /// <returns>A map of channel name to status for channels found.</returns>
    public async Task<IDictionary<string, Models.ChannelStatus>> BulkGetChannelStatusAsync(
        string database,
        string schema,
        string pipe,
        IEnumerable<string> channelNames,
        CancellationToken cancellationToken = default)
    {
        EnsureIngestReady();
        var path = $"/v2/streaming/databases/{Uri.EscapeDataString(database)}/schemas/{Uri.EscapeDataString(schema)}/pipes/{Uri.EscapeDataString(pipe)}:bulk-channel-status";
        var uri = Combine(_ingestBaseUri!, path);
        var payload = JsonSerializer.Serialize(new { channel_names = channelNames }, _json);
        var (resp, body) = await SendWithRetryAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            AddAuth(req);
            return req;
        }, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(resp, body);
        var map = new Dictionary<string, ChannelStatus>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("channel_statuses", out var statuses) || doc.RootElement.TryGetProperty("channelStatuses", out statuses))
        {
            foreach (var prop in statuses.EnumerateObject())
            {
                var cs = prop.Value.Deserialize<ChannelStatus>(_json);
                if (cs is not null) map[prop.Name] = cs;
            }
        }
        return map;
    }

    /// <summary>
    /// Drops a channel and its metadata on the server. Internal use by SnowpipeChannel.
    /// </summary>
    /// <remarks>
    /// Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#drop-channel
    /// </remarks>
    /// <param name="database">Database name.</param>
    /// <param name="schema">Schema name.</param>
    /// <param name="pipe">Pipe name.</param>
    /// <param name="channelName">Channel name.</param>
    /// <param name="requestId">Optional request identifier (UUID) for tracing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task DropChannelInternalAsync(
        string database,
        string schema,
        string pipe,
        string channelName,
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureIngestReady();
        var path = $"/v2/streaming/databases/{Uri.EscapeDataString(database)}/schemas/{Uri.EscapeDataString(schema)}/pipes/{Uri.EscapeDataString(pipe)}/channels/{Uri.EscapeDataString(channelName)}";
        var uri = Combine(_ingestBaseUri!, path, requestId is null ? null : ($"requestId={Uri.EscapeDataString(requestId.Value.ToString())}"));
        var (resp, body) = await SendWithRetryAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, uri);
            AddAuth(req);
            return req;
        }, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(resp, body);
    }

    /// <summary>
    /// Waits until the channel status indicates data is committed up to the expected point.
    /// This initial implementation performs a single check; polling can be added later.
    /// </summary>
    /// <remarks>
    /// Uses the Bulk Get Channel Status endpoint to monitor progress:
    /// https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#bulk-get-channel-status
    /// </remarks>
    /// <param name="database">Database name.</param>
    /// <param name="schema">Schema name.</param>
    /// <param name="pipe">Pipe name.</param>
    /// <param name="channelName">Channel name.</param>
    /// <param name="continuationToken">Continuation token to wait for (committed).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task CloseChannelWhenCommittedAsync(
        string database,
        string schema,
        string pipe,
        string channelName,
        string continuationToken,
        CancellationToken cancellationToken = default)
    {
        EnsureIngestReady();
        if (string.IsNullOrWhiteSpace(continuationToken)) return;

        var start = DateTimeOffset.UtcNow;
        var timeout = TimeSpan.FromSeconds(30); // default polling timeout
        var delay = TimeSpan.FromMilliseconds(200);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var statuses = await BulkGetChannelStatusAsync(database, schema, pipe, new[] { channelName }, cancellationToken).ConfigureAwait(false);
            if (statuses.TryGetValue(channelName, out var status))
            {
                if (!string.IsNullOrEmpty(status.LastCommittedOffsetToken) && string.Equals(status.LastCommittedOffsetToken, continuationToken, StringComparison.Ordinal))
                {
                    _logger?.LogDebug("Channel {Channel} caught up to token {Token}", channelName, continuationToken);
                    return;
                }
            }

            if (DateTimeOffset.UtcNow - start > timeout)
            {
                _logger?.LogWarning("Timeout waiting for commit for {Database}.{Schema}.{Pipe}.{Channel} token={Token}", database, schema, pipe, channelName, continuationToken);
                throw new SnowpipeException($"Timeout waiting for channel '{channelName}' to commit up to provided token.", System.Net.HttpStatusCode.RequestTimeout);
            }
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void EnsureIngestReady()
    {
        if (_ingestBaseUri is null) throw new InvalidOperationException("Ingest hostname not set. Call GetHostnameAsync + ExchangeScopedTokenAsync first.");
        if (string.IsNullOrEmpty(_scopedToken)) throw new InvalidOperationException("Scoped token not set. Call ExchangeScopedTokenAsync first.");
    }

    private void AddAuth(HttpRequestMessage req)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _scopedToken);
        req.Headers.TryAddWithoutValidation("X-Snowflake-Authorization-Token-Type", "OAuth");
    }

    private static Uri Combine(Uri baseUri, string path, string? query = null)
    {
        var builder = new UriBuilder(new Uri(baseUri, path));
        if (!string.IsNullOrEmpty(query)) builder.Query = query;
        return builder.Uri;
    }

    private static string Truncate(string s, int max = 512) => s.Length <= max ? s : s.Substring(0, max);

    private static string? TryGetRequestId(HttpResponseMessage resp, string body)
    {
        if (resp.Headers.TryGetValues("X-Request-Id", out var ridHeader))
        {
            var rid = ridHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(rid)) return rid;
        }
        try
        {
            if (!string.IsNullOrEmpty(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String)
                {
                    return rid.GetString();
                }
            }
        }
        catch { }
        return null;
    }

    private async Task<(HttpResponseMessage resp, string body)> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        TimeSpan baseDelay = TimeSpan.FromMilliseconds(200);
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var req = requestFactory();
            var started = DateTimeOffset.UtcNow;
            _logger?.LogDebug("{Method} {Uri}", req.Method, req.RequestUri);
            var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var reqId = TryGetRequestId(resp, body);
            _logger?.LogDebug("Response {Status} in {Ms}ms (reqId={ReqId})", (int)resp.StatusCode, (DateTimeOffset.UtcNow - started).TotalMilliseconds, reqId);

            int code = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode)
            {
                return (resp, body);
            }

            // Retry on 429 or 5xx
            if ((code == 429 || code >= 500) && attempt < maxAttempts)
            {
                TimeSpan delay = resp.Headers.RetryAfter?.Delta ?? TimeSpan.Zero;
                if (delay == TimeSpan.Zero)
                {
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                    delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)) + jitter;
                }
                _logger?.LogDebug("Retrying attempt {Attempt} after {Delay} due to status {Status}", attempt + 1, delay, code);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }
            if (!resp.IsSuccessStatusCode)
            {
                _logger?.LogWarning("HTTP {Status} after {Attempts} attempt(s) for {Method} {Uri} (reqId={ReqId})", code, attempt, req.Method, req.RequestUri, reqId);
            }
            return (resp, body);
        }
        throw new InvalidOperationException("Unreachable retry loop exit.");
    }

    private static void EnsureSuccessOrThrow(HttpResponseMessage resp, string body)
    {
        if (resp.IsSuccessStatusCode) return;
        var status = resp.StatusCode;
        string? errorCode = null;
        string? message = null;
        string? requestId = null;
        try
        {
            if (!string.IsNullOrEmpty(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("error_code", out var ec) && ec.ValueKind == JsonValueKind.String)
                    errorCode = ec.GetString();
                if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    message = msg.GetString();
                if (root.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String)
                    requestId = rid.GetString();
            }
        }
        catch
        {
            // Ignore parse errors; fall back to status reason
        }
        message ??= body?.Length > 0 ? body : resp.ReasonPhrase ?? $"HTTP {(int)status}";
        // Enrich message with useful identifiers from the server
        var parts = new List<string>();
        parts.Add($"status={(int)status}");
        if (!string.IsNullOrEmpty(errorCode)) parts.Add($"code={errorCode}");
        if (!string.IsNullOrEmpty(requestId)) parts.Add($"requestId={requestId}");
        if (resp.Headers.TryGetValues("X-Request-Id", out var ridHeader))
        {
            var rid = ridHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(rid) && rid != requestId) parts.Add($"x-request-id={rid}");
        }
        if (parts.Count > 0) message = $"{message} (" + string.Join(", ", parts) + ")";

        switch ((int)status)
        {
            case 400:
                throw new SnowpipeBadRequestException(message, HttpStatusCode.BadRequest, errorCode, requestId);
            case 401:
            case 403:
                throw new SnowpipeUnauthorizedException(message, (HttpStatusCode)status, errorCode, requestId);
            case 404:
                throw new SnowpipeNotFoundException(message, HttpStatusCode.NotFound, errorCode, requestId);
            case 429:
                TimeSpan? retryAfter = null;
                if (resp.Headers.RetryAfter?.Delta is not null) retryAfter = resp.Headers.RetryAfter!.Delta;
                throw new SnowpipeRateLimitException(message, retryAfter, errorCode, requestId);
        }

        if ((int)status >= 500)
            throw new SnowpipeServerException(message, status, errorCode, requestId);

        throw new SnowpipeException(message, status, errorCode, requestId);
    }

    /// <summary>Disposes underlying resources.</summary>
    public void Dispose()
    {
        if (_ownsHttp)
        {
            try { _http.Dispose(); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes underlying resources asynchronously.</summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
