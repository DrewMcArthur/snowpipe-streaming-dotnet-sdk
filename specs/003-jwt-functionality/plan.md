# Implementation Plan: JWT Key Pair Authentication

Status: Ready
Feature: specs/003-jwt-functionality/spec.md

## Goals
- Enable apps to authenticate using Snowflake key pair auth by generating the JWT in application code from a private key and static identifiers.
- Make it cloud‑friendly: read secrets from environment/providers, avoid manual steps and on‑disk tokens.
- Keep the flow simple and well‑documented.

## Non‑Goals
- OAuth and PAT flows (out of scope for this feature).
- Persisting secrets or tokens to disk.

## Assumptions
- Account identifier and user are known and can be uppercased safely for the JWT claims.
- Private key is provided as PEM (PKCS#8), optionally encrypted with a passphrase.
- The app can compute the RSA public key fingerprint (SHA‑256) from the provided private key.

## References
- specs/003-jwt-functionality/reference/snowflake_jwt_docs.txt (Generate a JWT token)

## Approach
1) Introduce a credentials/configuration model for key pair auth (account identifier, user, private key source, passphrase, optional fingerprint).
2) Add a JWT generator utility to build and sign tokens per Snowflake format (iss/sub/iat/exp) and RS256 signature.
3) Compute the SHA‑256 fingerprint of the public key (or accept a provided fingerprint) and prefix with "SHA256:".
4) Integrate with the client so account‑host calls use the generated JWT and set X‑Snowflake‑Authorization‑Token‑Type: KEYPAIR_JWT.
5) Provide configuration via environment variables first; allow injection for secret providers without taking on cloud SDK dependencies.
6) Document the end‑to‑end flow and security guidance.

## Tasks
- T1 [Design] Define `KeyPairAuthOptions` (account identifier, user, private key source (PEM string or path), optional passphrase, optional fingerprint, token lifetime, clock skew tolerance).
- T2 [Code] Implement `JwtGenerator` that:
  - Parses PKCS#8 RSA private key (encrypted or not) and derives the RSA public key.
  - Computes the SHA‑256 fingerprint and formats it as `SHA256:<base64url>`.
  - Builds JWT payload with iss, sub, iat, exp (≤1 hour) and signs with RS256.
- T3 [Code] Add a pluggable token source for `SnowpipeClient` account‑host calls (e.g., `IAccountTokenProvider`) that returns a fresh JWT; set header `X-Snowflake-Authorization-Token-Type: KEYPAIR_JWT`.
- T4 [Code] Provide an environment‑backed provider that reads secrets (`SNOWFLAKE_ACCOUNT`, `SNOWFLAKE_USER`, `SNOWFLAKE_PRIVATE_KEY`, `SNOWFLAKE_PRIVATE_KEY_PATH`, `SNOWFLAKE_PRIVATE_KEY_PASSPHRASE`).
- T5 [Tests] Contract tests verify:
  - Authorization header contains a valid RS256 JWT with correct iss/sub casing and timing claims.
  - Optional header KEYPAIR_JWT is present.
  - Encrypted key path works when passphrase is supplied; fails clearly otherwise.
- T6 [Docs] Update README and quickstart with cloud‑friendly setup using env vars and an example showing in‑app JWT generation (no external tooling).
- T7 [Docs] Security guidance: do not log secrets; keep tokens in memory; recommended lifetimes and clock skew.

## Risks & Mitigations
- Risk: Misformatted `iss` or casing causes unauthorized.
  - Mitigation: Normalize casing, validate inputs, unit tests on claim formats.
- Risk: Key parsing failures across platforms.
  - Mitigation: Add tests for encrypted/unencrypted keys; document supported formats.
- Risk: Secret leakage via logs or exceptions.
  - Mitigation: Scrub sensitive values from logs; throw messages without embedding secrets.

## Acceptance Criteria
- Application can authenticate using only private key and static identifiers from secrets; no manual JWT generation.
- Generated JWT conforms to Snowflake requirements (iss/sub/iat/exp, 1h max lifetime, uppercase ACCOUNT/USER, SHA256:fingerprint).
- Headers include `Authorization: Bearer <jwt>` and `X-Snowflake-Authorization-Token-Type: KEYPAIR_JWT` on account endpoints.
- Documentation provides clear setup for cloud workloads and security guidance.
