# CLAUDE.md

.NET 10 Clean Architecture API (Domain, Application, Infrastructure, Api) with
DDD and CQRS Lite. Every rule is enforced by the build, an architecture test or
`scripts/verify`. Details live in `.claude/rules/` (index below).

## 1. No-Hallucination Policy (non-negotiable, applies to every step)

- Never guess a package name, version, API, config key, CLI flag or file syntax.
- Verify every NuGet package and version with `dotnet package search <id> --exact-match`
  or official docs BEFORE adding it. Never write versions from memory.
- If unsure, write "UNVERIFIED: <what>" and ask. Do not invent.
- Mark every assumption as "ASSUMPTION:" in reports.
- Never say "done" or "pass" without running the real command and showing its output.
- Report clearly what was NOT done or could NOT be verified.
- If a required package does not support net10.0 or its license, STOP and ask.
  Do not substitute silently.

## 2. Source of truth

- Decisions: `docs/adr/` (an accepted decision changes only through a new ADR).
- Architecture and conventions: `docs/architecture.md`, `docs/coding-standards.md`,
  `.claude/rules/`. Feature workflow: `docs/sample-flow.md`.
- If two sources conflict, ask the developer; do not choose silently.

## 3. Non-negotiable rules (summary)

- Dependency rule: Domain -> nothing; Application -> Domain; Infrastructure ->
  Application + Domain; Api composes. No EF Core in Application or Domain.
- CQRS Lite with own interfaces (no MediatR). Commands: write repository +
  aggregate method + `IUnitOfWork`. Queries: only `I<Name>ReadRepository`
  returning DTOs.
- Aggregates: `Create(...)` with guard clauses, private setters, state changes
  as methods returning `Result` when user input can make them fail.
- Expected failures return `Result` with an `Error` code; unexpected ones throw.
  The Api maps errors once (`ErrorResults.ToProblem`) to ProblemDetails with
  `errorCode`. Success returns the raw DTO or `PagedResult<T>`.
- DI style by project: `Configure*` on `IHostApplicationBuilder` in
  Application/Infrastructure; `Add*` / `Use*` in Api; nothing inline in
  `Program.cs`.
- Every action has `[Authorize]` or `[AllowAnonymous]` (anonymous only in the
  Sample namespace) and `[ProducesResponseType]` for every status.
- Options with `ValidateOnStart()` for all settings; secrets only in
  user-secrets or environment variables; `TimeProvider`, never `DateTime.UtcNow`.
- Sort only by whitelisted keys; page size capped by Options.
- `Encrypt=false` only in Development/Testing; never disable TLS certificate
  validation.
- Warnings are errors; suppressions are scoped and carry a reason.
- Honest `?` annotations (NullGuard enforces them at runtime); `Task<T>`, not
  `ValueTask<T>`, for non-nullable async results.

## 4. Commits

Prefixes: `feat:`, `fix:`, `docs:`. One logical change per commit.

## 5. Working on features

- **Add a feature:** copy the Sample slice; follow `docs/sample-flow.md`
  ("Add a new feature by copying Sample"). New endpoints use `[Authorize]`.
- **When controllers or DTOs change:** regenerate `docs/openapi.json` with
  `bash scripts/generate-openapi.sh` and commit it; verify fails otherwise.
- **When Sample is removed:** delete its files, then delete
  `src/Infrastructure/Persistence/Migrations/` and recreate the initial
  migration for the remaining model; regenerate `docs/openapi.json`
  (checklist in `docs/sample-flow.md`).
- **Model changes:** add a migration (command in `src/Infrastructure/CLAUDE.md`).
- **Decision changes:** add or supersede an ADR in `docs/adr/`.

## 6. Rule files (`.claude/rules/`)

| File | Content |
| --- | --- |
| `architecture.md` | Layers, dependency rule, read/write paths, DI style |
| `cqrs.md` | Handler interfaces, pipeline, use-case layout |
| `ddd.md` | Aggregates, value objects, errors, guard clauses |
| `patterns.md` | Patterns with "use when" / "do NOT use when" |
| `naming.md` | Projects, use cases, errors, Docker names |
| `security.md` | Identity, authorization, secrets, TLS, API surface |
| `testing.md` | Test stack, projects, test-only analyzer rules |
| `packages.md` | Verifying packages, CPM, vulnerabilities, forbidden packages |
| `nullguard.md` | Null-safety mode, flags, known gap |
| `formatting.md` | `dotnet format`, the whitespace-only edit hook |
| `anti-patterns.md` | Forbidden practices and what to do instead |

Each `src/<Layer>/CLAUDE.md` holds that layer's strict rules and DI style.
Agents: `architect` (plans changes), `reviewer` (read-only review).

## 7. Verify commands

- Full check (also the only CI step): `bash scripts/verify.sh`
- Windows: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
  (tested with Windows PowerShell 5.1; not tested with PowerShell 7).
- Steps: restore + Release build (0 warnings) -> `dotnet format
  --verify-no-changes` -> `dotnet test` -> vulnerable/deprecated packages ->
  placeholder check -> naming and TLS checks -> OpenAPI freshness (generated
  twice, compared) -> `docker build`.
- Quick loop while coding: `dotnet build -c Release`, `dotnet test --solution
  Datavanced.SmartTaskManagement.slnx`.
- The edit hook fixes whitespace only; run verify for the full style check.
