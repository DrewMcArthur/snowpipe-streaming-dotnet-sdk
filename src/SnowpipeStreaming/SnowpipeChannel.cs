using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SnowpipeStreaming;

/// <summary>
/// A channel bound to a specific database/schema/pipe and channel name. Provides ergonomic append and lifecycle APIs.
/// </summary>
public class SnowpipeChannel : IAsyncDisposable, IDisposable
{
    private readonly SnowpipeClient _client;
    private readonly bool _dropOnDispose;
    private readonly System.Threading.SemaphoreSlim _appendLock = new(1, 1);

    /// <summary>Database name.</summary>
    public string Database { get; }
    /// <summary>Schema name.</summary>
    public string Schema { get; }
    /// <summary>Pipe name.</summary>
    public string Pipe { get; }
    /// <summary>Channel name.</summary>
    public string Name { get; }
    /// <summary>The latest continuation token returned by the service. Updated after each append.</summary>
    public string? LatestContinuationToken { get; internal set; }

    internal SnowpipeChannel(SnowpipeClient client, string database, string schema, string pipe, string name, string? initialContinuationToken, bool dropOnDispose)
    {
        _client = client;
        Database = database;
        Schema = schema;
        Pipe = pipe;
        Name = name;
        LatestContinuationToken = initialContinuationToken;
        _dropOnDispose = dropOnDispose;
    }

    /// <summary>
    /// Appends pre-serialized NDJSON lines, splitting requests to respect the 16MB payload limit. Updates <see cref="LatestContinuationToken"/>.
    /// </summary>
    public async Task<string> AppendRowsAsync(IEnumerable<string> ndjsonLines, string? offsetToken = null, Guid? requestId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(LatestContinuationToken)) throw new InvalidOperationException("No continuation token set. Open the channel before appending.");
        await _appendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await AppendInternal(ndjsonLines, offsetToken, requestId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _appendLock.Release();
        }
    }

    /// <summary>
    /// Appends strongly-typed rows (serialized to NDJSON), splitting requests to respect the 16MB payload limit. Updates <see cref="LatestContinuationToken"/>.
    /// </summary>
    public async Task<string> AppendRowsAsync<T>(IEnumerable<T> rows, string? offsetToken = null, Guid? requestId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(LatestContinuationToken)) throw new InvalidOperationException("No continuation token set. Open the channel before appending.");
        await _appendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await AppendInternal(rows, offsetToken, requestId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _appendLock.Release();
        }
    }

    private async Task<string> AppendInternal(IEnumerable<string> lines, string? offsetToken, Guid? requestId, CancellationToken ct)
    {
        var next = await _client.AppendRowsAsync(Database, Schema, Pipe, Name!, LatestContinuationToken!, lines, offsetToken, requestId, ct).ConfigureAwait(false);
        LatestContinuationToken = next;
        return next;
    }

    private async Task<string> AppendInternal<T>(IEnumerable<T> rows, string? offsetToken, Guid? requestId, CancellationToken ct)
    {
        var next = await _client.AppendRowsAsync(Database, Schema, Pipe, Name!, LatestContinuationToken!, rows, offsetToken, requestId, ct).ConfigureAwait(false);
        LatestContinuationToken = next;
        return next;
    }

    /// <summary>
    /// Waits until the channel's committed offset catches up to the specified continuation token (or the latest token if not specified).
    /// </summary>
    public Task WaitForCommitAsync(string? token = null, CancellationToken cancellationToken = default)
    {
        var target = token ?? LatestContinuationToken ?? throw new InvalidOperationException("No token available to wait for.");
        return _client.CloseChannelWhenCommittedAsync(Database, Schema, Pipe, Name, target, cancellationToken);
    }

    /// <summary>
    /// Drops the channel on the server.
    /// </summary>
    public Task DropAsync(CancellationToken cancellationToken = default) => _client.DropChannelAsync(Database, Schema, Pipe, Name, null, cancellationToken);

    /// <summary>
    /// Gets the latest committed offset token for this channel.
    /// </summary>
    public async Task<string?> GetLatestCommittedOffsetTokenAsync(CancellationToken cancellationToken = default)
    {
        var map = await _client.BulkGetChannelStatusAsync(Database, Schema, Pipe, new[] { Name }, cancellationToken).ConfigureAwait(false);
        return map.TryGetValue(Name, out var status) ? status.LastCommittedOffsetToken : null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_dropOnDispose)
        {
            try
            {
                // Wait for the latest known token to commit, then drop the channel.
                await WaitForCommitAsync().ConfigureAwait(false);
                await DropAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _client.Logger?.LogError(ex, "Error during SnowpipeChannel DisposeAsync cleanup (wait+drop) for {Database}.{Schema}.{Pipe}.{Channel}", Database, Schema, Pipe, Name);
                // Swallow errors during disposal to avoid throwing from DisposeAsync.
            }
        }
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_dropOnDispose)
        {
            try
            {
                // Synchronous disposal path: block on async operations.
                WaitForCommitAsync().GetAwaiter().GetResult();
                _client.DropChannelAsync(Database, Schema, Pipe, Name).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _client.Logger?.LogError(ex, "Error during SnowpipeChannel Dispose cleanup (wait+drop) for {Database}.{Schema}.{Pipe}.{Channel}", Database, Schema, Pipe, Name);
                // Swallow errors during disposal to avoid throwing from Dispose.
            }
        }
        GC.SuppressFinalize(this);
    }
}
