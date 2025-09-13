# Tasks: Snowpipe Streaming .NET SDK (C#)

**Input**: Design documents from `/specs/001-this-is-a/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)
```
1. Load plan.md from feature directory
   → If not found: ERROR "No implementation plan found"
   → Extract: tech stack, libraries, structure
2. Load optional design documents:
   → data-model.md: Extract entities → model tasks
   → contracts/: Each file → contract test task
   → research.md: Extract decisions → setup tasks
3. Generate tasks by category:
   → Setup: project init, dependencies, linting
   → Tests: contract tests, integration tests
   → Core: models, services, client methods
   → Integration: logging, retries, cancellation
   → Polish: unit tests, docs, packaging
4. Apply task rules:
   → Different files = mark [P] for parallel
   → Same file = sequential (no [P])
   → Tests before implementation (TDD)
5. Number tasks sequentially (T001, T002...)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness:
   → All contracts have tests?
   → All entities have models?
   → All endpoints implemented?
9. Return: SUCCESS (tasks ready for execution)
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
- Single project layout
```
src/
└── SnowpipeStreaming/
    ├── SnowpipeClient.cs
    ├── Models/
    ├── Serialization/
    └── Internal/

tests/
├── contract/
├── integration/
└── unit/
```

## Phase 3.1: Setup
- [ ] T001 Create solution and projects: `SnowpipeStreaming.sln`, `src/SnowpipeStreaming/SnowpipeStreaming.csproj`, `tests/contract/Contract.Tests.csproj`, `tests/integration/Integration.Tests.csproj`, `tests/unit/Unit.Tests.csproj`
- [ ] T002 Add dependencies: tests → `xunit`, `xunit.runner.visualstudio`, `FluentAssertions`; library → `Microsoft.Extensions.Logging.Abstractions` (optional) in `src/SnowpipeStreaming/SnowpipeStreaming.csproj`
- [ ] T003 Populate contracts from docs (verbatim endpoints, headers, schemas) in `specs/001-this-is-a/contracts/*.yaml`
- [ ] T004 [P] Configure formatting/analyzers: `.editorconfig`, enable `nullable` + `warningsaserrors` in `src/SnowpipeStreaming/SnowpipeStreaming.csproj`

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
- [ ] T005 [P] Contract test: get hostname in `tests/contract/HostnameTests.cs`
- [ ] T006 [P] Contract test: exchange scoped token in `tests/contract/TokenExchangeTests.cs`
- [ ] T007 [P] Contract test: open channel request/response shape in `tests/contract/OpenChannelTests.cs`
- [ ] T008 [P] Contract test: append rows request/response shape in `tests/contract/AppendRowsTests.cs`
- [ ] T009 [P] Contract test: bulk channel status in `tests/contract/BulkChannelStatusTests.cs`
- [ ] T010 [P] Contract test: drop channel in `tests/contract/DropChannelTests.cs`
- [ ] T011 Integration test: end-to-end flow (acquire → open → append → wait/close → drop) in `tests/integration/BasicFlowTests.cs`
- [ ] T012 (Defer) Add retry/backoff tests in a later iteration

## Phase 3.3: Core Implementation (ONLY after tests are failing)
- [ ] T013 [P] Implement DTOs per data model in `src/SnowpipeStreaming/Models/*` (Token, Channel*, Append*, ErrorResponse)
- [ ] T014 [P] Serialization options (camelCase, enums) in `src/SnowpipeStreaming/Serialization/JsonOptions.cs`
- [ ] T015 Internal HTTP transport (base URI, headers, auth) in `src/SnowpipeStreaming/Internal/HttpSnowflakeTransport.cs`
- [ ] T016 Implement `SnowpipeClient.GetHostnameAsync(...)` in `src/SnowpipeStreaming/SnowpipeClient.cs`
- [ ] T017 Implement `SnowpipeClient.ExchangeScopedTokenAsync(...)` in `src/SnowpipeStreaming/SnowpipeClient.cs`
- [ ] T018 Implement `SnowpipeClient.OpenChannelAsync(...)` in `src/SnowpipeStreaming/SnowpipeClient.cs`
- [ ] T019 Implement `SnowpipeClient.AppendRowsAsync(...)` in `src/SnowpipeStreaming/SnowpipeClient.cs`
- [ ] T020 Implement `SnowpipeClient.BulkGetChannelStatusAsync(...)` and `CloseChannelWhenCommittedAsync(...)` in `src/SnowpipeStreaming/SnowpipeClient.cs`
- [ ] T021 Implement `SnowpipeClient.DropChannelAsync(...)` in `src/SnowpipeStreaming/SnowpipeClient.cs`
- [ ] T022 (Defer) Retry policy with exponential backoff + jitter for 429/5xx in `src/SnowpipeStreaming/Internal/RetryPolicy.cs`

## Phase 3.4: Integration
- [ ] T019 Wire optional logging via `ILogger` (trace requests, response codes) in `src/SnowpipeStreaming/*`
- [ ] T020 Cancellation and timeout configuration plumbed through all public APIs in `src/SnowpipeStreaming/SnowpipeClient.cs`
- [ ] T021 Document thread-safety and sequencing guarantees in `docs/usage/thread-safety.md`

## Phase 3.5: Polish
- [ ] T023 [P] Unit tests: serialization and model validation in `tests/unit/SerializationTests.cs`
- [ ] T024 (Defer) [P] Unit tests: retry policy/backoff timing in `tests/unit/RetryPolicyTests.cs`
- [ ] T025 [P] Update `specs/001-this-is-a/quickstart.md` with finalized API signatures
- [ ] T026 XML docs for public APIs; generate package metadata in `src/SnowpipeStreaming/SnowpipeStreaming.csproj`
- [ ] T027 Prepare NuGet packaging (SemVer pre-release) in `src/SnowpipeStreaming/SnowpipeStreaming.csproj`

## Phase 3.6: DX & CI
- [ ] T028 Ergonomic error types and response mapping in `src/SnowpipeStreaming/Errors/*` and `SnowpipeClient`
- [ ] T029 [P] Contract tests for error handling (401 JWT missing/invalid, 401 scoped token invalid, 400 error payload mapping, 5xx server) in `tests/contract/*`
- [ ] T030 Add `README.md` describing library and creation process (Specify + OpenAI Codex)
- [ ] T031 Add `.gitignore` for .NET builds/artifacts
- [ ] T032 Add GitHub Actions CI workflow to build and test on push/PR

## Dependencies
- Tests (T005–T010) before implementation (T011–T018)
- T011 blocks T014–T017
- T013 blocks T014–T017
- T018 blocks integration tests passing reliably
- Implementation before polish (T022–T026)

## Parallel Example
```
# Launch T005–T008 together:
Contract test: IngestCredentialsTests.cs
Contract test: OpenChannelTests.cs
Contract test: AppendRowsTests.cs
Contract test: ChannelStatusTests.cs
```

## Notes
- [P] tasks = different files, no dependencies
- Verify tests fail before implementing
- Commit after each task
- Keep public methods small and single-responsibility; mirror REST spec exactly

## Validation Checklist
- [ ] All contracts have corresponding tests
- [ ] All entities have model tasks
- [ ] All tests come before implementation
- [ ] Parallel tasks truly independent
- [ ] Each task specifies exact file path
- [ ] No task modifies same file as another [P] task
