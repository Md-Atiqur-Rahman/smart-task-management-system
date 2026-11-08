# Project context: Smart Task Management System

Updated 2026-11-09. This file describes the current truth, not the history. Anything marked **proposed** has not been confirmed by the developer.

## At a glance

**বাংলা সারাংশ.** এটা একটা hiring assignment: .NET 10 (Clean Architecture) backend ও Angular frontend দিয়ে একটা Smart Task Management System বানাতে হবে। Code quality, security ও documentation feature সংখ্যার চেয়ে বেশি গুরুত্বপূর্ণ। একটা reusable template ও তা থেকে project বানানোর একটা Claude Code skill আগেই তৈরি ও প্রমাণিত (CI সবুজ)। এই project repo টা সেই template থেকে বানানো হয়েছে। এখনো কোনো আসল feature (Auth, Project, Task, Dashboard, AI, Angular) লেখা হয়নি। প্রথম কাজ হলো এই project এর নিজের `docs/PRD.md` লেখা, developer এর সাথে একটা একটা করে প্রশ্ন করে। Auth নিজের implementation হবে (Keycloak এখন না)। Developer বাংলা পড়েন; সিদ্ধান্ত সহজ ভাষায় বুঝিয়ে বলুন।

**English.** Build a Smart Task Management System (ASP.NET Core 10 and Angular) as a hiring assignment judged on architecture, code quality, security and documentation. A verified Clean Architecture template exists, and this repository was created from it. No business feature exists yet. The first task is to write this project's own `docs/PRD.md` by interviewing the developer. Authentication will be implemented in-house.

## Assignment requirements

The full text is in `docs/assignment.md` (checked word for word against the original PDF). The assignment stresses code quality, security, maintainability and best practices over implementing every possible feature. Summary:

| Area | Required |
| --- | --- |
| Stack | ASP.NET Core 9 or 10, EF Core, SQL Server, JWT, Swagger/OpenAPI; Angular 18+ with standalone components |
| Auth | Register, login, logout, JWT, refresh tokens, role-based authorization; roles Admin, Project Manager, Team Member |
| Projects | Create, update, delete, view, list, search, sort, paginate |
| Tasks | Create, update, delete, assign, status (To Do, In Progress, Completed, Cancelled), priority (Low, Medium, High, Critical), due date, search, sort, paginate |
| Dashboard | Total projects, total tasks, tasks by status, tasks by priority, completed versus pending, upcoming due tasks |
| AI feature | One endpoint that improves a task description: fix grammar, improve clarity, more professional, expand short text, actionable. GitHub Models (free) or any free provider |
| Architecture | N-layer or Clean Architecture; SOLID; design patterns where appropriate |
| Security | JWT, refresh tokens, password hashing, role-based authorization, HTTPS, CORS, input validation, basic rate limiting, secure configuration |
| Backend | Async/await, FluentValidation, Swagger, global exception handling, Serilog, health checks, consistent API responses |
| Frontend | Login, dashboard, project management, task management, search, responsive UI; lazy loading, route guards, HTTP interceptors, reactive forms |
| Documentation | `README.md` (overview, setup, stack, API overview, folder structure) and `PROMPTS.md` (AI prompt design, structure, example inputs and outputs, validation approach, safety considerations) |
| Deliverables | GitHub repository link, SQL script or EF Core migrations, README, PROMPTS.md, API collection (Postman or Swagger export) |

Evaluation: architecture, code quality, security, backend, frontend, design patterns, documentation, readability and maintainability.

## Current state

The foundation is finished and verified; every Smart Task feature is still to be built.

### Repositories (all private on GitHub)

| Repository | Purpose | State |
| --- | --- | --- |
| `dotnet-clean-architecture-boilerplate` | Reusable template with neutral names (`Contoso` / `TemplateApp`) and rename scripts `scripts/new-project.sh` and `.ps1` | Marked as a GitHub template repository, CI green. Has `docs/TEMPLATE-PRD.md` (what the template contains and why) and a README with two ways to create a project |
| `claude-skills` | The Claude Code skill `new-project` that creates a verified project from the template (asks for the git identity, shows the parsed inputs, waits for a yes, never pushes) | Installed in the developer's user skills folder; README written |
| `smart-task-management-system` (this repository) | The assignment project, created from the template and renamed to `Datavanced.SmartTaskManagement` | CI green |

A fourth private repository is an archive of the long template build history and its planning documents. It is not needed to continue the work.

### What exists in this repository

- Four source projects (Domain, Application, Infrastructure, Api) and three test projects (UnitTests, IntegrationTests, ArchitectureTests), plus a small `tests/WeavingProbe` library used only to probe NullGuard behaviour.
- A runnable `Sample` feature (5 endpoints: create, list with search/sort/paging, get by id, complete, soft delete). It is the copy-source for every new feature. See `docs/sample-flow.md`.
- 172 tests that pass, including a real SQL Server through Testcontainers and architecture rules that fail the build when layering is broken.
- `scripts/verify.sh` and `scripts/verify.ps1`: build with 0 warnings, format check, tests, vulnerable and deprecated package check, naming and secret checks, OpenAPI freshness against the committed `docs/openapi.json`, and `docker build`. Both pass locally; CI runs `verify.sh` on Ubuntu.
- Docker Compose with SQL Server bound to loopback, and a working multi-stage Dockerfile that runs as a non-root user.
- Claude Code files: `CLAUDE.md`, `.claude/rules/`, one `CLAUDE.md` per layer, `architect` and read-only `reviewer` agents, and a whitespace-only format hook.
- ADRs in `docs/adr/`, `docs/architecture.md`, `docs/coding-standards.md`.
- `docs/TEMPLATE-PRD.md`, inherited from the template. The project should write its own `docs/PRD.md`; `TEMPLATE-PRD.md` may then be deleted.

### Not started

The project PRD, the user and role model and the Auth feature, Project, Task, Dashboard, the AI endpoint, the Angular frontend, `PROMPTS.md`, the Postman or Swagger export for the final submission, and the project-specific README.

## Locked decisions and why

These are settled. Changing one needs an ADR and the developer's approval.

| Area | Decision | Reason |
| --- | --- | --- |
| Platform | .NET 10, `.slnx` solution, SQL Server with EF Core, Windows development, Docker available | Assignment allows .NET 9 or 10 |
| Architecture | Clean Architecture with DDD building blocks; CQRS Lite with own handler interfaces, no MediatR | MediatR is now commercial; own interfaces keep the pattern visible |
| Write side | One repository per aggregate root plus `IUnitOfWork`, both declared in Domain | Classic DDD |
| Read side | `I<Name>ReadRepository` declared in Application, returning DTOs, implemented in Infrastructure with hand-written `Select` projections | Keeps EF Core out of Application and Domain |
| List queries | Query object with reusable search, sort and paging helpers; sort fields from a whitelist; page size capped from Options | Prevents injection through sort fields and huge pages |
| Errors | `Result` for expected failures; abstract `AppException` for unexpected or invariant failures; `ProblemDetails` with an `errorCode` | One mapping point from error type to HTTP status |
| Responses | Raw DTO on success, `PagedResult<T>` for lists, no wrapper envelope | Standard, works with Swagger; an ADR explains the trade-off |
| Status mapping | NotFound 404, Conflict 409, Validation 400, Forbidden 403, Unauthorized 401 | Defined once in the Api layer |
| Security context | `ISecurityContext` and `ISecurityContextSetter` in Application; the setter runs once in one middleware after JWT validation | Handlers never touch HttpContext or tokens |
| Password hashing | ASP.NET Core Identity `PasswordHasher` behind own `IPasswordHasher` | Proven algorithm, no extra dependency in Application |
| DI style | Application and Infrastructure: `Configure*` extension on `IHostApplicationBuilder`. Api: `Add*` and `Use*` extensions. Program.cs registers nothing inline | One rule per project, enforced by architecture tests |
| Null safety | `Nullable` enabled plus Fody with NullGuard.Fody in mode `NullableReferenceTypes`; use `Task<T>`, not `ValueTask<T>`, for non-nullable async results | NullGuard does not check non-nullable `ValueTask<T>` |
| Formatting | `dotnet format` only; `.editorconfig` is the single source; naming rules at severity suggestion | One formatter, no conflicts |
| Tests | xunit.v3 on Microsoft.Testing.Platform, NSubstitute, Shouldly, NetArchTest.Rules, Testcontainers (SQL Server) | Free licenses; real database in integration tests |
| Database TLS | `Encrypt=False` only in Development and Testing; the literal `TrustServerCertificate` is forbidden everywhere | Certificate validation is never disabled |
| OpenAPI | `docs/openapi.json` is committed and checked for freshness in CI; no Spectral | Doubles as the Swagger export deliverable |
| Auth | Implemented in-house: JWT access token plus rotating refresh token. Keycloak was considered and not chosen for now | The assignment lists these features as deliverables |

### Banned

AutoMapper (version 14.0.0 has advisory GHSA-rvv3-g6hj-g44x; fixed versions are commercial), MediatR, FluentAssertions, Moq, CSharpier, the Specification pattern, caching, API versioning, Domain Events, Aspire, StyleCop, Meziantou, AsyncFixer, VS.Threading, commitlint, Renovate, Spectral, SonarQube, NuGet lock files, coverage packages with unclear licenses, any EF Core reference in Domain or Application.

A pinned transitive package (`System.Security.Cryptography.Pkcs` 10.0.14) fixes a vulnerable dependency of EF Core; it is explained in an ADR and should be removed when upstream updates.

## Working rules for any AI agent

1. **No-Hallucination Policy.** Never guess a package, version, API, config key, CLI flag or file syntax. Verify packages with `dotnet package search` and official docs before adding them. Mark anything unsure as `UNVERIFIED` and ask. Mark assumptions as `ASSUMPTION`.
2. **Evidence before claims.** Never say done or pass without running the real command and showing its output. Report what was not done or could not be verified. Never pipe a script through `tail` or similar in a way that hides its exit code.
3. **Plan first.** Produce a plan, wait for approval, then work in small steps with one commit per task and a stop at each milestone.
4. **Tests first.** Write the failing test, show it failing, then implement.
5. **Stop on surprises.** If a package is incompatible, a rule conflicts with a framework, or an analyzer fires, stop and show the options; do not suppress silently. Fix real code issues. Pre-approved global disables are only CA2007, CS1591 and CA1062; every other exception is scoped and has a reason comment.
6. **Verify before pushing.** Run `scripts/verify` and confirm it passes, after the change is committed (the OpenAPI freshness check compares with the committed file). Push normally; never force-push or rewrite history. Do not push without the developer's go-ahead.
7. **Secrets.** Never commit secrets. Values live in user-secrets or `.env` (git-ignored); `.env.example` holds only safe placeholders. Scan the diff before pushing.
8. **Git identity.** Commit with the repository-local identity the developer configured (a GitHub no-reply address). Never change the global git config and never copy values from it. Check `git log -1 --format="%an <%ae>"` after the first commit.
9. **No unrelated content.** Do not copy code, names or packages from other employers or private projects into this repository.
10. **Features follow the Sample slice.** Copy the structure in `docs/sample-flow.md` and keep the architecture tests green. When controllers or DTOs change, regenerate `docs/openapi.json`.
11. **One PRD.** Keep exactly one project PRD, `docs/PRD.md`. After the developer approves it, any change goes into its change log with a date, what and why, and needs approval.

### Working with the developer

The developer reads Bangla and prefers explanations in simple Bangla with English technical terms. When they say they do not understand, answer shortly and simply with an example. Ask one clear question at a time and propose a default. They run commands themselves and paste the output back, so give exact commands and say which terminal to use (Git Bash for `.sh` scripts; in PowerShell a plain `bash` may resolve to WSL and fail; the PowerShell scripts were tested with Windows PowerShell 5.1 only). A sign-in window of the Git credential manager may hide behind other windows during clone or push.

## What to do next

The developer confirmed in-house Auth. Everything below is **proposed** until the developer confirms it in the PRD interview.

### First task: the project PRD

Write `docs/PRD.md` by interviewing the developer, one question at a time, each with a recommended default. Order of topics: roles and permissions, task status rules, project delete behaviour, the AI provider, the frontend location, where the refresh token lives. Mark every requirement CONFIRMED or PROPOSED, and anything about the AI provider that cannot be verified from official documentation as UNVERIFIED. The PRD is final only when the developer approves it and its open-questions list is empty.

### Build order

1. Auth (users, roles, register, login, refresh, logout)
2. Project module
3. Task module
4. Dashboard
5. AI endpoint
6. Angular frontend (after the backend is stable)
7. README, `PROMPTS.md`, API collection export

### Auth design (proposed)

| Topic | Design |
| --- | --- |
| Tokens | Short-lived JWT access token (for example 15 minutes) and a long-lived random refresh token (for example 7 days); lifetimes in Options |
| Storage | Refresh tokens are stored only as a hash in a `RefreshToken` table, never in plain text; generated with a cryptographic random generator |
| Rotation | Every refresh revokes the used token and issues a new pair |
| Reuse detection | If a revoked refresh token is presented again, revoke all refresh tokens of that user |
| Logout | Revokes the refresh token; the access token expires on its own |
| Register | Default role Team Member; only an Admin can grant Admin or Project Manager; the first Admin is seeded from secret configuration, never hardcoded |
| Login | Same generic error for unknown email and wrong password; rate limited |
| Claims | `sub` and `role`; role comes only from the validated token, never from the request body |
| Layers | Domain: `User`, `RefreshToken`, `Role`; Application: commands, validators, `ITokenService`, repositories; Infrastructure: JWT generation, refresh-token hashing, migration; Api: `AuthController` and authorization policies |

The template already provides `ISecurityContext`, the security-context middleware, `IPasswordHasher`, JWT options validation (signing key of at least 32 characters) and rate limiting.

### Domain rules (proposed)

- Project and Task are separate aggregates; a Task references its project only by `ProjectId`.
- Task status transitions: To Do to In Progress or Cancelled; In Progress to Completed, Cancelled or To Do; Completed and Cancelled are final. An invalid transition returns a Conflict (409) `Result`, not an exception.
- Deleting a project is a soft delete and hides its tasks.
- Permissions: Admin sees and manages everything; a Project Manager manages their own projects and tasks and can assign; a Team Member sees only tasks assigned to them (and their projects) and may change only the status of those tasks.
- Dashboard numbers use database-level grouping and are scoped by role (Admin all, Project Manager own projects, Team Member own tasks).
- AI endpoint: behind an `IAiProvider` strategy; usable by Admin and Project Manager; input length limit, timeout, clear failure response, and prompt-injection caution. Prompt design is documented in `PROMPTS.md`.

### Frontend (proposed)

Angular 18 or later, standalone components, lazy-loaded feature routes, auth and role guards, interceptors (attach token, refresh on 401, global error handling), reactive forms, responsive layout. Location: a `client/` folder in the same repository.

## Open questions and UNVERIFIED items

| Question | Proposed default |
| --- | --- |
| Is the Angular app in the same repository? | Yes, in `client/`, after the backend is stable |
| Where does Angular keep the refresh token (memory, localStorage, httpOnly cookie)? | Decide in the frontend phase; localStorage is exposed to XSS, a cookie needs CORS and CSRF handling |
| Are the proposed domain rules and permissions correct? | Confirm one by one in the PRD interview |
| Which GitHub Models model and endpoint will the AI feature use? | Unknown; verify from official documentation |
| Must the repository be public or shared with the evaluator? | Check the assignment instructions; keep it private until then |

UNVERIFIED:

- The `.ps1` scripts under PowerShell 7 (tested only with Windows PowerShell 5.1), and Linux or macOS support for the scripts.
- Whether `dotnet package list` reports an unreachable vulnerability feed as a problem; restore-time NuGet audit is the second guard.
- The GitHub Models API shape, authentication and rate limits for the AI feature.
- Whether an evaluator accepts in-house Auth as expected; the developer may ask the assignment contact if unsure.

Template follow-ups (not blocking): GitHub Actions are pinned by major version, not by commit SHA; `ubuntu-latest` moves to new Ubuntu releases and may change behaviour; behind a reverse proxy, rate limiting sees the proxy address, so configure forwarded headers with explicit trusted proxies before production.

## Repo map, commands and verification

### Layout

```text
Datavanced.SmartTaskManagement.slnx
src/Domain  src/Application  src/Infrastructure  src/Api
tests/UnitTests  tests/IntegrationTests  tests/ArchitectureTests  tests/WeavingProbe
docs/            assignment, architecture, coding standards, sample-flow, ADRs, openapi.json
scripts/         verify.sh, verify.ps1, check-packages, generate-openapi, new-project
.claude/         rules, agents, settings and the format hook
CLAUDE.md        main rules; each src project has its own CLAUDE.md
```

Assemblies and namespaces are `Datavanced.SmartTaskManagement.<Layer>`, set once in `Directory.Build.props`. Package versions live only in `Directory.Packages.props`.

### Commands

| Task | Command |
| --- | --- |
| Full verification (Git Bash) | `bash scripts/verify.sh` |
| Full verification (Windows PowerShell 5.1) | `powershell -File scripts/verify.ps1` |
| Start the database | `docker compose up -d db` (needs a local `.env` copied from `.env.example`) |
| Local secrets | Set `Database:Name`, `Database:Password` and `Jwt:SigningKey` with `dotnet user-secrets set` on `src/Api`; the exact one-liner is in the README |
| Run the API | `dotnet run --project src/Api --launch-profile http`, then open `/swagger` (Development only; the template used port 5199) |
| Regenerate the OpenAPI file | `bash scripts/generate-openapi.sh` and commit `docs/openapi.json` |
| Full stack in Docker | `docker compose up -d` (local only; the compose file runs in Development mode; the API port comes from `API_PORT` in `.env`) |

Docker Desktop must be running for the integration tests and `docker build`.

### Definition of done for any task

The build has 0 warnings, all tests pass, `dotnet format --verify-no-changes` is clean, `scripts/verify` passes after the commit, architecture tests stay green, and `docs/openapi.json` is regenerated when the API surface changed. The agent shows the real output of these commands.
