# Research: Snowpipe Streaming .NET SDK

## Decisions
- Target Frameworks: [NEEDS CLARIFICATION] Propose `net8.0` (LTS). Consider `netstandard2.1` if broader runtime support is required.
- JSON Serialization: `System.Text.Json` with camelCase, ignore nulls only if spec allows; strict enum/string mappings per spec.
- HTTP: Single shared `HttpClient` instance with configurable `HttpMessageHandler` injection; default reasonable timeouts.
- Retries/Backoff: Exponential backoff with jitter for transient HTTP 429/5xx; max attempts configurable; align to Snowflake rate-limit guidance. [NEEDS CLARIFICATION]
- Logging: Optional `ILogger` via `Microsoft.Extensions.Logging.Abstractions` to avoid hard dependency. [NEEDS CLARIFICATION]
- Thread Safety: Client is safe for concurrent use for independent channels; per-channel sequencing guarantees documented.

## Rationale
- .NET 8 provides performance and long-term support.
- System.Text.Json is the default modern JSON stack; avoid extra deps.
- Clear separation of transport and serialization improves testability.

## Alternatives Considered
- Newtonsoft.Json for features like non-camel policies — deferred unless spec requires.
- Polly for retries — may implement simple built-in policy to reduce deps.

## Unknowns to Resolve
- Exact endpoint URIs, required headers, error codes/messages (to be transcribed from Snowflake docs into contracts/).
- Limits: maximum rows/payload per append; channel concurrency constraints.
- JWT format expectations (client receives token vs must mint); clock skew tolerance.
- Status polling cadence and close semantics (what conditions define "caught up").

## Notes from Reference
- Global headers: `Authorization` (scoped token), optional `X-Snowflake-Authorization-Token-Type` (JWT/OAuth).
- Max request payload: 16 MB per request.
- Endpoints confirmed: hostname (GET), token exchange (POST form), open channel (PUT), append rows (POST NDJSON), drop channel (DELETE), bulk channel status (POST).
