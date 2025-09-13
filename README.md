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
See `specs/001-this-is-a/quickstart.md` for an end-to-end sample.

## Lifecycle and Channels
- Use `OpenChannelAsync(..., dropOnDispose: true)` to get an `await using`-friendly `Channel` that automatically waits for the latest continuation token to commit and then drops the channel server-side upon disposal. This is ideal for ephemeral jobs and tests.
- For long-lived channels, omit `dropOnDispose` and manage lifecycle explicitly via `channel.WaitForCommitAsync()` and `channel.DropAsync()`.
- Disposal never throws: cleanup errors are swallowed to keep `using`/`await using` ergonomic. If you need to handle errors, call the methods explicitly before disposal.

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
This repository was bootstrapped using a structured specification workflow:
- The feature was specified and planned using a "Specify" repository template with AI-friendly templates (see `specs/` directory).
- Implementation was assisted by OpenAI’s Codex CLI (open-source agentic coding interface), which generated scaffolding, plans, tasks, and code under human direction, using the Snowflake documentation provided locally.

## Status
- Contract and integration tests run against a mock server that emulates the documented behaviors.
- Retries/backoff and polling semantics are deferred to a later iteration.

## License
TBD.
