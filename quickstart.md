# Quickstart: Snowpipe Streaming .NET SDK

This library provides a C# client for Snowflake Snowpipe Streaming REST endpoints.

## Basic Usage

```csharp
// 1) Construct client with account URL and JWT (provided by caller)
var client = new SnowpipeClient(
    accountUrl: new Uri("https://<account>.<region>.snowflakecomputing.com"),
    jwt: "<jwt-token>");

// 2) Discover hostname and exchange scoped token
var hostname = await client.GetHostnameAsync(cancellationToken);
await client.ExchangeScopedTokenAsync(hostname, cancellationToken);

// 3) Open a channel for a target pipe (returns a Channel)
await using var channel = await client.OpenChannelAsync(
    database: "DB",
    schema: "SCHEMA",
    pipe: "PIPE",
    channelName: "my_channel",
    dropOnDispose: true,
    cancellationToken);
// channel.LatestContinuationToken is set after open

// 4) Append rows (generic serialization; auto-splits >16MB into multiple requests)
var rows = new[]{ new { id = 1, value = "a" }, new { id = 2, value = "b" } };
var continuation = await channel.AppendRowsAsync(rows, cancellationToken: cancellationToken);

// 5) Close the channel and wait until status catches up
await channel.WaitForCommitAsync(continuation, cancellationToken);
```

Notes:
- All requests and responses adhere strictly to the Snowflake REST spec (see `specs/001-this-is-a/contracts/`).
- Methods are cancellation-aware; retries/backoff follow Snowflake guidance.
- Client is safe for concurrent appends across channels; sequence ordering per channel is preserved.
