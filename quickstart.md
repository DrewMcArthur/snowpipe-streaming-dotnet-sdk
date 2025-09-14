# Quickstart: Snowpipe Streaming .NET SDK

This library provides a C# client for Snowflake Snowpipe Streaming REST endpoints.

## Basic Usage

```csharp
// Option A) Key Pair Authentication (in-app JWT from env)
// Set env vars: SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, and SNOWFLAKE_PRIVATE_KEY or _PATH (and optional _PASSPHRASE)
var client = new SnowpipeClient(
    accountUrl: new Uri("https://<account>.<region>.snowflakecomputing.com"),
    accountTokenProvider: new SnowpipeStreaming.Auth.EnvironmentKeyPairTokenProvider());

// Discover hostname and exchange scoped token
var hostname = await client.GetHostnameAsync(cancellationToken);
await client.ExchangeScopedTokenAsync(hostname, cancellationToken);

// Option B) Bring your own JWT (manual)
// 1) Construct client with account URL and JWT (provided by caller)
var client = new SnowpipeClient(
    accountUrl: new Uri("https://<account>.<region>.snowflakecomputing.com"),
    jwt: "<jwt-token>");

// 2) Discover hostname and exchange scoped token
var hostname = await client.GetHostnameAsync(cancellationToken);
await client.ExchangeScopedTokenAsync(hostname, cancellationToken);

// Open a channel for a target pipe (returns a Channel)
await using var channel = await client.OpenChannelAsync(
    database: "DB",
    schema: "SCHEMA",
    pipe: "PIPE",
    channelName: "my_channel",
    dropOnDispose: true,
    cancellationToken);
// channel.LatestContinuationToken is set after open

// Append rows (generic serialization; auto-splits >16MB into multiple requests)
var rows = new[]{ new { id = 1, value = "a" }, new { id = 2, value = "b" } };
var continuation = await channel.AppendRowsAsync(rows, cancellationToken: cancellationToken);

// Close the channel and wait until status catches up
await channel.WaitForCommitAsync(continuation, cancellationToken);
```

Notes:
- All requests and responses adhere strictly to the Snowflake REST spec (see `specs/001-this-is-a/contracts/`).
- Methods are cancellation-aware; retries/backoff follow Snowflake guidance.
- Client is safe for concurrent appends across channels; sequence ordering per channel is preserved.
