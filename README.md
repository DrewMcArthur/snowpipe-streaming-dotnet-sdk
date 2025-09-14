# Snowpipe Streaming .NET SDK (C#)

[![CI](https://github.com/drewmcarthur/snowpipe-streaming-dotnet-sdk/actions/workflows/ci.yml/badge.svg)](https://github.com/drewmcarthur/snowpipe-streaming-dotnet-sdk/actions/workflows/ci.yml)

A lightweight, idiomatic C# library for interacting with Snowflake's Snowpipe Streaming REST endpoints. It follows the official "Snowpipe Streaming API REST endpoints" documentation and mirrors the ergonomics of the Java/Python SDKs while providing robust types and a clean, single-responsibility API surface for .NET 8.

## Features
- Account vs Ingest host handling (discovers ingest host via `/v2/streaming/hostname`)
- Token exchange for scoped access (`/oauth/token`)
- Open channel, append rows (NDJSON), bulk channel status, drop channel
- User-Agent and Authorization headers set correctly
- Ergonomic error mapping with rich exceptions
- Cancellation-friendly async APIs and optional logging via `ILogger`

## Quickstart
See `./quickstart.md` for an end-to-end sample. For cloud workloads, you can use key pair auth with in-app JWT generation via environment variables.

Key Pair Authentication (in-app JWT)
- Set the following environment variables with your secrets:
  - `SNOWFLAKE_ACCOUNT` — account identifier (uppercase; omit region if using account locator)
  - `SNOWFLAKE_USER` — user name (uppercase)
  - Either `SNOWFLAKE_PRIVATE_KEY` (PKCS#8 PEM contents) or `SNOWFLAKE_PRIVATE_KEY_PATH` (path to PKCS#8 PEM)
  - Optional: `SNOWFLAKE_PRIVATE_KEY_PASSPHRASE` for encrypted keys

Usage example:
```csharp
using SnowpipeStreaming;
using SnowpipeStreaming.Auth;

var accountUrl = new Uri("https://<account>.<region>.snowflakecomputing.com");
var provider = new EnvironmentKeyPairTokenProvider();
var client = new SnowpipeClient(accountUrl, provider);

// Discover ingest hostname and exchange for a scoped token
var hostname = await client.GetHostnameAsync();
await client.ExchangeScopedTokenAsync(hostname);

// Ready for ingest operations
await using var ch = await client.OpenChannelAsync("DB", "SCHEMA", "PIPE", "my_channel", dropOnDispose: true);
var next = await ch.AppendRowsAsync(new [] { new { id = 1 } });
await ch.WaitForCommitAsync(next);
```

## Lifecycle and Channels
- Use `OpenChannelAsync(..., dropOnDispose: true)` to get an `await using`-friendly `Channel` that automatically waits for the latest continuation token to commit and then drops the channel server-side upon disposal. This is ideal for ephemeral jobs and tests.
- For long-lived channels, omit `dropOnDispose` and manage lifecycle explicitly via `channel.WaitForCommitAsync()` and `channel.DropAsync()`.
- Disposal never throws: cleanup errors are swallowed to keep `using`/`await using` ergonomic. If you need to handle errors, call the methods explicitly before disposal.

## Thread Safety & Concurrency
- Appends on the same channel must be sequential due to continuation tokens. The library serializes `AppendRowsAsync` per channel with an internal semaphore to prevent concurrent appends.
- Multiple channels can append in parallel.

## Retry & Backoff
- Transient errors (HTTP 429 and 5xx) automatically retry up to 3 attempts with exponential backoff + jitter. The retry respects `Retry-After` when present.
- Errors still surface as typed exceptions with enriched context (`error_code`, `requestId`, `x-request-id`).

## API Overview

SnowpipeClient (control + ingest wiring)
- `SnowpipeClient(Uri accountUrl, IAccountTokenProvider provider, ...)` — Use a custom account-host token provider (e.g., key-pair JWT) for account endpoints.
- `Task<string> GetHostnameAsync(...)` — Discover ingest host for the account.
  - Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#get-hostname
- `Task ExchangeScopedTokenAsync(string hostname, ...)` — Exchange JWT for scoped token; sets ingest base URI.
  - Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#exchange-scoped-token
- `Task<SnowpipeChannel> OpenChannelAsync(string database, string schema, string pipe, string channelName, string? offsetToken = null, Guid? requestId = null, bool dropOnDispose = false, ...)` — Create or open a channel; returns `SnowpipeChannel` with initial continuation token.
  - Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#open-channel
- `Task<IDictionary<string, Models.ChannelStatus>> BulkGetChannelStatusAsync(string database, string schema, string pipe, IEnumerable<string> channelNames, ...)` — Bulk channel status lookup.
  - Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#bulk-get-channel-status
- `SnowpipeChannel.DropAsync()` — Drop a channel from the channel instance.
  - After dropping, the channel enters a terminal state and further operations like `AppendRowsAsync`, `WaitForCommitAsync`, or status checks throw `InvalidOperationException`.
  - Docs: https://docs.snowflake.com/en/user-guide/snowpipe-streaming-high-performance-rest-api#drop-channel
- `Task CloseChannelWhenCommittedAsync(string database, string schema, string pipe, string channelName, string continuationToken, ...)` — Polls bulk status until committed to the given token (used by channel disposal & helpers).

Low-level appends (available but typically use SnowpipeChannel)
- `Task<string> AppendRowsAsync(string database, string schema, string pipe, string channelName, string continuationToken, IEnumerable<string> ndjsonLines, ...)`
- `Task<string> AppendRowsAsync<T>(..., IEnumerable<T> rows, ...)` — Serializes to NDJSON and splits payloads to respect 16MB.

SnowpipeChannel (ergonomic per-channel API)
- `Task<string> AppendRowsAsync(IEnumerable<string> ndjsonLines, ...)` — NDJSON lines; auto-splits to ≤16MB and updates `LatestContinuationToken`.
- `Task<string> AppendRowsAsync<T>(IEnumerable<T> rows, ...)` — Generic rows; serialized to NDJSON, auto-splits, updates token.
- `Task<string?> GetLatestCommittedOffsetTokenAsync(...)` — Convenience to fetch the latest committed offset for this channel.
- `Task WaitForCommitAsync(string? token = null, ...)` — Wait until committed equals the given (or latest) token.
- `Task DropAsync(...)` — Drop the channel.
- `await using var channel = await client.OpenChannelAsync(..., dropOnDispose: true)` — On dispose: waits for commit and drops server-side; errors swallowed.

Notes
- Headers: account endpoints send `Authorization: Bearer <jwt>` + `X-Snowflake-Authorization-Token-Type: JWT`; ingest endpoints send `Authorization: Bearer <scoped>` + `X-Snowflake-Authorization-Token-Type: OAuth`.
  - When using key pair auth provider, account endpoints send `X-Snowflake-Authorization-Token-Type: KEYPAIR_JWT`.
- Content types: token exchange uses `application/x-www-form-urlencoded`; append rows uses `application/x-ndjson` with `continuationToken` query.

Security notes
- Do not log private keys or passphrases. Avoid writing secrets to disk.
- JWT lifetime is capped at 1 hour by Snowflake; the provider refreshes before expiry.
- Ensure system clocks are reasonably synchronized to avoid iat/exp validation issues.

Example:
```csharp
await using var channel = await client.OpenChannelAsync(
    database: "DB", schema: "SCHEMA", pipe: "PIPE", channelName: "my_channel", dropOnDispose: true);
var next = await channel.AppendRowsAsync(new [] { new { id = 1 } });
// On dispose: waits for commit and drops the channel
```

## Install
This repository is in active development. Packaging to NuGet is planned (see tasks).

## Development
- Requirements: .NET 8 SDK
- Build & test: `dotnet build` and `dotnet test`
- Projects:
  - `src/SnowpipeStreaming` — library
  - `tests/contract`, `tests/integration`, `tests/unit` — tests

## How This Was Created
This repository was bootstrapped using a structured specification workflow, described [here](https://github.com/github/spec-kit):
- The feature was specified and planned using a "Specify" repository template with AI-friendly templates (see `specs/` directory).
- Implementation was assisted by OpenAI’s Codex CLI (open-source agentic coding interface), which generated scaffolding, plans, tasks, and code under human direction, using the Snowflake documentation provided locally.

## Status
- Contract and integration tests run against a mock server that emulates the documented behaviors.
- Retries/backoff and polling semantics are deferred to a later iteration.

## License
TBD.
