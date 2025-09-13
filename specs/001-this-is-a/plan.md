# Implementation Plan: Snowpipe Streaming .NET SDK (C#)

**Branch**: `001-this-is-a` | **Date**: 2025-09-13 | **Spec**: /Users/drewmca/Coding/snowpipe-streaming-dotnet-sdk/specs/001-this-is-a/spec.md
**Input**: Feature specification from `/specs/001-this-is-a/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
4. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
5. Execute Phase 1 → contracts, data-model.md, quickstart.md
6. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
7. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
8. STOP - Ready for /tasks command
```

## Summary
- Build a clean, single-responsibility C# client library that conforms exactly to Snowflake's "Snowpipe Streaming API REST Endpoints" guide.
- Provide a client that, given an account URL and a caller-provided JWT, acquires stream ingest credentials, then supports: open channel, append rows, and close channel by waiting until channel status catches up to the requested appends.
- Mirror ergonomics of the Python/Java SDKs while being idiomatic C# with robust types and clear separation of concerns.

## Technical Context
**Language/Version**: C# 12 targeting .NET 8 (LTS) [NEEDS CLARIFICATION: confirm target frameworks; consider multi-target `net8.0` + `netstandard2.1`]
**Primary Dependencies**: `System.Net.Http` (HttpClient), `System.Text.Json` (JSON), optional `Microsoft.Extensions.Logging.Abstractions` (logging) [NEEDS CLARIFICATION]
**Storage**: N/A (stateless client; no persistence)
**Testing**: xUnit + FluentAssertions [NEEDS CLARIFICATION]
**Target Platform**: .NET 8 (Windows/Linux/macOS) [NEEDS CLARIFICATION]
**Project Type**: single
**Performance Goals**: High-throughput append with backpressure; minimize allocations; parallelism bounded by spec [NEEDS CLARIFICATION: concrete throughput/latency targets]
**Constraints**: Must follow Snowflake REST spec to the letter; strict type fidelity for request/response; strong input validation; predictable retries/backoff; thread-safe client usage guidance.
**Scale/Scope**: Typical enterprise ingestion workloads; channel lifecycle across many appends [NEEDS CLARIFICATION]

## Constitution Check
**Simplicity**:
- Projects: 2 (library `src/`, tests `tests/`) — within limit
- Use framework directly: Yes (HttpClient, System.Text.Json)
- Single data model: Yes (DTOs for REST payloads only)
- Avoid patterns: Yes (no repositories/UoW)

**Architecture**:
- Feature as library: Yes (`SnowpipeStreaming`)
- Libraries listed: 1 (client + DTOs + serialization)
- CLI per library: N/A for now (library focus)
- Library docs: quickstart.md planned

**Testing (NON-NEGOTIABLE)**:
- RED-GREEN-Refactor: Enforced in tasks
- Commit order: tests before implementation
- Order: Contract → Integration → Unit
- Real dependencies: Use live HTTP contract tests via recorded fixtures or a mock server [NEEDS CLARIFICATION]
- Integration tests: for client flows and error handling
- Forbidden: implementation before failing tests

**Observability**:
- Structured logging via `ILogger` optional [NEEDS CLARIFICATION]
- Error context mapped to Snowflake error shapes

**Versioning**:
- Versioning: Semantic (0.x pre-release initially) [NEEDS CLARIFICATION]
- BUILD increments per change via CI [NEEDS CLARIFICATION]
- Breaking changes: SemVer constraints

## Project Structure

### Documentation (this feature)
```
specs/001-this-is-a/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
src/
├── SnowpipeStreaming/
│   ├── SnowpipeClient.cs
│   ├── Channels/
│   ├── Models/          # DTOs per REST spec
│   ├── Serialization/
│   └── Internal/
tests/
├── contract/
├── integration/
└── unit/
```

**Structure Decision**: Option 1 (single project) per Technical Context

## Phase 0: Outline & Research
1. Extract unknowns from Technical Context:
   - Target frameworks (`net8.0`, `netstandard2.1`?)
   - Logging abstraction (`ILogger` vs none)
   - Retry and backoff policy (align with spec’s rate limits)
   - Exact endpoint paths, required headers, status semantics (to be transcribed verbatim)
   - JSON serialization casing and null-handling rules per spec
   - JWT usage: client receives JWT vs generates — confirm no signing needed
   - Concurrency model for appends; max payload size; idempotency tokens

2. Research tasks (record findings in research.md):
   - Decisions for TFMs, dependencies, JSON options
   - Error taxonomy mapping from Snowflake to custom exceptions
   - Backoff strategy (exponential jitter) consistent with Snowflake guidance

3. Consolidate findings in `research.md` with Decision/Rationale/Alternatives.

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts
1. Extract entities → `data-model.md`:
   - CredentialRequest/CredentialResponse
   - ChannelOpenRequest/Response (channel name, table, offset token)
   - AppendRequest/Response (rows payload, token/sequence)
   - ChannelStatus (committed offset, latest sequence, state)
   - Error model (code, message, details)

2. Generate API contracts (OpenAPI YAML) under `/contracts/`:
   - Ingest credentials
   - Open channel
   - Append rows
   - Get channel status / Close semantics

3. Generate contract tests skeletons (not implemented here; created by /tasks):
   - Assert request/response schemas match contracts

4. Extract test scenarios → quickstart.md:
   - Acquire credentials → Open channel → Append → Wait/Close

5. Agent file: not included at this stage (library focus).

**Output**: data-model.md, /contracts/*, quickstart.md

## Phase 2: Task Planning Approach
**Task Generation Strategy**:
- Use `/templates/tasks-template.md` as base
- Generate tasks from contracts, data model, quickstart
- Mark [P] where files are independent

**Ordering Strategy**:
- TDD: tests before implementation
- Models → Services → Client methods → Docs

**Estimated Output**: ~20–30 tasks in tasks.md

