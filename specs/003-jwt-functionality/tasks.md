# Tasks: JWT Key Pair Authentication

**Input**: Design documents from `/specs/003-jwt-functionality/`
**Prerequisites**: plan.md (required), reference/snowflake_jwt_docs.txt

## Phase 1: Setup
- [ ] T001 Create auth folder structure in `src/SnowpipeStreaming/Auth/` [P]
- [ ] T002 Add test asset folder `tests/assets/keys/` with sample PKCS#8 keys (unencrypted + encrypted) and README about test-only usage [P]

## Phase 2: Tests First (TDD)
- [ ] T003 Contract test: Account call includes `Authorization: Bearer <jwt>` and `X-Snowflake-Authorization-Token-Type: KEYPAIR_JWT` in `tests/contract/KeyPairAuthHeaderTests.cs` [P]
- [ ] T004 Unit test: JWT generator builds claims with correct `iss`, `sub`, `iat`, `exp≤1h`, and RS256 signature in `tests/unit/JwtGeneratorTests.cs` [P]
- [ ] T005 Unit test: Encrypted private key requires passphrase; wrong passphrase fails cleanly in `tests/unit/JwtGeneratorEncryptedKeyTests.cs` [P]
- [ ] T006 Unit test: Public key SHA-256 fingerprint computation and `SHA256:<fp>` formatting in `tests/unit/PublicKeyFingerprintTests.cs` [P]
- [ ] T007 Unit test: Identifier normalization (uppercase ACCOUNT and USER; reject region-qualified account locator) in `tests/unit/IdentifierNormalizationTests.cs` [P]

## Phase 3: Core Implementation (ONLY after tests are failing)
- [ ] T008 Define `KeyPairAuthOptions` (account identifier, user, private key source (PEM string or path), optional passphrase, optional fingerprint, token lifetime, clock skew tolerance) in `src/SnowpipeStreaming/Auth/KeyPairAuthOptions.cs` [P]
- [ ] T009 Implement `PublicKeyFingerprint` helper to compute SHA-256 from RSA public key in `src/SnowpipeStreaming/Auth/PublicKeyFingerprint.cs` [P]
- [ ] T010 Implement `JwtGenerator` (parse PKCS#8, handle encrypted keys, build iss/sub/iat/exp claims, sign RS256) in `src/SnowpipeStreaming/Auth/JwtGenerator.cs` [P]
- [ ] T011 Add `IAccountTokenProvider` and `KeyPairJwtTokenProvider` that uses options + generator to produce fresh JWTs in `src/SnowpipeStreaming/Auth/KeyPairJwtTokenProvider.cs` [P]
- [ ] T012 Integrate provider into `SnowpipeClient` for account-host requests; set `X-Snowflake-Authorization-Token-Type: KEYPAIR_JWT` in `src/SnowpipeStreaming/SnowpipeClient.cs` (make provider injectable without breaking existing ctor) 
- [ ] T013 Add environment-backed provider `EnvironmentKeyPairTokenProvider` reading `SNOWFLAKE_ACCOUNT`, `SNOWFLAKE_USER`, `SNOWFLAKE_PRIVATE_KEY` or `_PATH`, and optional `SNOWFLAKE_PRIVATE_KEY_PASSPHRASE` in `src/SnowpipeStreaming/Auth/EnvironmentKeyPairTokenProvider.cs` [P]

## Phase 4: Integration & Polish
- [ ] T014 Wire sample usage in `quickstart.md` showing env var configuration and in-app JWT generation (no manual steps)
- [ ] T015 Update `README.md` with key-pair auth section: required identifiers, header expectations, security notes
- [ ] T016 Ensure sensitive values are never logged; scrub exceptions where needed in `src/SnowpipeStreaming` (avoid printing key/passphrase)
- [ ] T017 Add XML documentation for all new public types/members (satisfy CS1591)

## Dependencies
- T003–T007 (tests) before T008–T013 (implementation)
- T010 depends on T009
- T011 depends on T008 and T010
- T012 depends on T011
- T014–T017 after implementation

## Parallel Examples
- Run T003–T007 in parallel (different files)
- Implement T008, T009, T010 in parallel; then T011; then T012
- Docs tasks (T014–T015) can proceed in parallel once API is stable

## Notes
- Key material in `tests/assets/keys/` must be test-only and non-sensitive. Document provenance and rotate if needed.
- Cap JWT lifetime at 1 hour regardless of option; enforce or clamp in generator.
- Normalize ACCOUNT and USER to uppercase; reject or normalize account locator with regions per docs.
- Do not persist JWTs or keys to disk; keep in memory only.
