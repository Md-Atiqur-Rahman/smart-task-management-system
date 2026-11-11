# ADR 0009: Committed docs/openapi.json, generated at build time and checked by verify

- Status: Accepted
- Date: 2026-11-08

## Context

The API contract must not drift from the code, and the check must run locally
and in CI without a running server or a database. No external OpenAPI linter (such as Spectral) is used.

## Decision

- The document comes from `Microsoft.AspNetCore.OpenApi` 10.0.14 (OpenAPI 3.1 by
  default on .NET 10). Build-time generation uses
  `Microsoft.Extensions.ApiDescription.Server` 10.0.14
  (https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0).
- Generation is OFF on normal builds. The property that switches it was read
  from the package's own `build/Microsoft.Extensions.ApiDescription.Server.targets`:
  `OpenApiGenerateDocumentsOnBuild` defaults to `$(OpenApiGenerateDocuments)`
  (true) only when empty. `Api.csproj` sets it to `false` unless
  `-p:GenerateOpenApiDocument=true`. Proven: a normal build writes no file.
- `scripts/generate-openapi.sh` / `.ps1` are the one-line regenerate command and
  the only callers of that property. Output: `docs/openapi.json`
  (`--file-name openapi --document-name v1`).
- The generator launches the app's entry point, so `ValidateOnStart` runs.
  The scripts pass the SAFE placeholders from `.env.example` as environment
  variables for that process only (`Database__Name`, `Database__Password`,
  `Jwt__SigningKey`) and `ASPNETCORE_ENVIRONMENT=Production` (no dev-only
  migration, no database connection). Proven: without the placeholders,
  generation fails with `OptionsValidationException`.
- `scripts/verify` generates twice, compares the two SHA-256 hashes
  (determinism), requires the file to be committed, and fails when
  `git diff` shows a change.

## Evidence

- Two runs: `97cb3c96c0ce102d45a68e3b9016f6c4ef2fbd7942e88b2f59f78b18a6c30458`
  both times, from `verify.sh` and `verify.ps1`.
- A temporary `[ProducesResponseType(418)]` produced a different (still
  deterministic) hash `0d2a0184...` and `VERIFY FAILED: docs/openapi.json is out
  of date`, exit 1.
- **False success found:** when the generator fails (e.g. missing settings), the
  `dotnet build` still reports success. The scripts therefore delete the file
  first and fail when it is missing, empty, or not a valid OpenAPI JSON
  document after the build.

## Consequences

- Every change to controllers or DTOs needs `bash scripts/generate-openapi.sh`
  and a commit of `docs/openapi.json`; verify enforces it.
- Phase 2 adds per-operation Bearer security requirements (see the Phase 2
  checklist in `docs/sample-flow.md`), which changes the document on purpose.
