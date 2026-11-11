# Project context: Smart Task Management System

Updated 2026-11-11. This file describes the current truth, not the history. Anything marked **proposed** has not been confirmed by the developer.

## At a glance

**বাংলা সারাংশ.** এটা একটা hiring assignment: .NET 10 (Clean Architecture) backend ও Angular frontend দিয়ে একটা Smart Task Management System বানাতে হবে। Code quality, security ও documentation feature সংখ্যার চেয়ে বেশি গুরুত্বপূর্ণ। একটা reusable template ও তা থেকে project বানানোর একটা Claude Code skill আগেই তৈরি ও প্রমাণিত (CI সবুজ)। এই project repo টা সেই template থেকে বানানো হয়েছে। Auth feature (register, login, refresh, logout, role, Redis) এবং Project module (create, update, delete, list, owner change) তৈরি হয়েছে; Task module (project এর ভিতরে task, assign, status বদল, filter) ও তৈরি হয়েছে; Dashboard (summary ও upcoming, দুটি endpoint) তৈরি হয়েছে; Notifications (stored list, live SSE push, email outbox) তৈরি হয়েছে; Angular frontend তৈরি হয়ে main-এ merge হয়েছে (PR #7); project README ও `PROMPTS.md` লেখা হয়েছে; AI endpoint `feat/ai-description` branch-এ চলছে। প্রথম কাজ হলো এই project এর নিজের `docs/PRD.md` লেখা, developer এর সাথে একটা একটা করে প্রশ্ন করে। Auth নিজের implementation হবে (Keycloak এখন না)। Developer বাংলা পড়েন; সিদ্ধান্ত সহজ ভাষায় বুঝিয়ে বলুন।

**English.** Build a Smart Task Management System (ASP.NET Core 10 and Angular) as a hiring assignment judged on architecture, code quality, security and documentation. A verified Clean Architecture template exists, and this repository was created from it. Authentication is built in-house (`SmartTaskManagement-Backend/docs/auth-design.md`) and the Project, Task, Dashboard and Notifications modules are built (`SmartTaskManagement-Backend/docs/project-design.md`, `SmartTaskManagement-Backend/docs/task-design.md`, `SmartTaskManagement-Backend/docs/dashboard-design.md`, `SmartTaskManagement-Backend/docs/notifications-design.md`). The Angular frontend is built and merged (`SmartTaskManagement-Frontend/`, design `SmartTaskManagement-Frontend/docs/frontend-design.md`). The project `README.md` and `SmartTaskManagement-Backend/PROMPTS.md` are written. The AI endpoint is in progress on `feat/ai-description`. The project PRD is `docs/PRD.md` (draft, not yet approved).

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

The foundation, the Auth feature, the Project module, the Task module, the Dashboard, the Notifications module and the Angular frontend are finished and verified. The AI endpoint is in progress.

### Repositories (all private on GitHub)

| Repository | Purpose | State |
| --- | --- | --- |
| `dotnet-clean-architecture-boilerplate` | Reusable template with neutral names (`Contoso` / `TemplateApp`) and rename scripts `scripts/new-project.sh` and `.ps1` | Marked as a GitHub template repository, CI green. Has `docs/TEMPLATE-PRD.md` (what the template contains and why) and a README with two ways to create a project |
| `claude-skills` | The Claude Code skill `new-project` that creates a verified project from the template (asks for the git identity, shows the parsed inputs, waits for a yes, never pushes) | Installed in the developer's user skills folder; README written |
| `smart-task-management-system` (this repository) | The assignment project, created from the template and renamed to `Datavanced.SmartTaskManagement` | CI green |

A fourth private repository is an archive of the long template build history and its planning documents. It is not needed to continue the work.

### What exists in this repository

- Four source projects (Domain, Application, Infrastructure, Api) and three test projects (UnitTests, IntegrationTests, ArchitectureTests), plus a small `tests/WeavingProbe` library used only to probe NullGuard behaviour.
- A runnable `Sample` feature (5 endpoints: create, list with search/sort/paging, get by id, complete, soft delete), all `[Authorize]`. It is the copy-source for every new feature. See `SmartTaskManagement-Backend/docs/sample-flow.md`.
- **Auth: built.** `AuthController` (`register`, `login`, `refresh`, `logout`, `me`; the only anonymous actions), `UsersController` (Admin: list users and change a role; Admin or Project Manager: assignable users), `User` and `RefreshToken` aggregates, a first-Admin seed from secret configuration, and `[RequireAllowedOrigin]` (Origin check against `Cors:AllowedOrigins`) on refresh and logout. The refresh token lives only in the `__Secure-refresh_token` HttpOnly cookie. Details: `SmartTaskManagement-Backend/docs/auth-design.md`, ADR 0013 to 0015.
- **Project: built.** `ProjectsController`: `POST` and `GET /api/projects` (list with search, sort by `name`/`createdAt`/`updatedAt`, paging, `ownerId` filter), `GET`, `PUT` and `DELETE /api/projects/{id}`, and `PUT /api/projects/{id}/owner` (Admin only). Create, update and delete: Admin or Project Manager; a Project Manager sees and changes only their own projects, Admin all, a Team Member (or any assignee) sees the projects of their tasks, details only (delivered by the Task module). Delete is a soft delete. The owner must be a Project Manager. Demotion guard: changing the role of a Project Manager who still owns projects returns 409 `users.owns_projects`. Design: `SmartTaskManagement-Backend/docs/project-design.md`.
- **Task: built.** `TaskItem` aggregate (`TaskItemStatus`, `TaskPriority`; priority Medium by default, date-only due date, no state machine) in the `Tasks` table. Endpoints: `POST` and `GET /api/projects/{projectId}/tasks`, `GET /api/tasks`, `GET`, `PUT` and `DELETE /api/tasks/{id}`, `PUT /api/tasks/{id}/status` (an Admin, the owning Project Manager, or the task's assignee) and `PUT /api/tasks/{id}/assignee` (Admin or owning Project Manager; null unassigns). Both lists filter by status, priority, `assigneeId`, `unassigned`, `dueFrom`/`dueTo` and sort rank-aware. The assignee (any role) sees the task and its project and may change its status. Project list and details carry task counts (an assignee-only caller counts only their own tasks). Design: `SmartTaskManagement-Backend/docs/task-design.md`.
- **Dashboard: built.** `DashboardController`: `GET /api/dashboard/summary` (total projects and tasks, tasks by status and by priority, completed, pending and overdue counts; all four status and priority keys are always present) and `GET /api/dashboard/upcoming` (pending tasks due from today to today + `Dashboard:UpcomingDays`, at most `Dashboard:UpcomingLimit`). Both are `[Authorize]`; every role sees only the projects and tasks it may see (one visibility definition shared with the project and task lists). Design: `SmartTaskManagement-Backend/docs/dashboard-design.md`.
- **Notifications: built.** `Notification` aggregate in the `Notifications` table, which is also the email outbox (ADR 0017). Five events (task assigned or unassigned, project owner assigned or removed; the actor is never notified). Endpoints: `GET /api/notifications`, `GET /api/notifications/unread-count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all` and `GET /api/notifications/stream` (SSE; event types `notification`, `notifications-read`, `dashboard-changed`, `heartbeat`; ends at the access token's expiry; browsers must use `fetch` with the bearer header). Live events go through Redis pub/sub to every instance (ADR 0018; fail open when Redis is down). An `EmailOutboxWorker` sends emails with MailKit (Mailpit locally, `http://localhost:8025`) and a `NotificationCleanupWorker` deletes notifications after `Notifications:RetentionDays` (7). Design: `SmartTaskManagement-Backend/docs/notifications-design.md`.
- **Frontend: built.** Angular 22 app in `SmartTaskManagement-Frontend/` (merged 2026-11-11, PR #7):
  - Screens: login and register, dashboard (live over SSE), projects, tasks, task detail, users and roles (Admin), notification bell with live toasts.
  - Security: access token in memory only; single-flight refresh on 401; HTTPS dev server.
  - Checks: `scripts/verify.sh` (lint, Prettier, strict type-check, 76 Vitest unit tests, production build, `npm audit`); Playwright E2E (`npm run e2e`, local only); CI `.github/workflows/frontend.yml`.
  - "Improve with AI" is shown disabled until the AI endpoint is merged.
  - Design: `SmartTaskManagement-Frontend/docs/frontend-design.md`; rules: `SmartTaskManagement-Frontend/CLAUDE.md`.
- **Submission documents: written.** The root `README.md` (overview, setup, stack, API overview, folder structure, deliverables) and `SmartTaskManagement-Backend/PROMPTS.md`; its section 4.2 holds the real smoke-test outputs (2026-11-12). API collection: `SmartTaskManagement-Backend/docs/openapi.json`.
- **Lesson from the Task module:** EF Core `IgnoreQueryFilters()` applies to the whole query, so every set in such a query needs its own explicit soft-delete predicate (`ProjectReadRepository` adds `!p.IsDeleted`).
- **Redis (exception to "no caching"):** it holds security state only, never cached query results (ADR 0013). Allowed uses: the per-user token version (`auth:token-version:<userId>`) and the shared per-IP rate-limit counter. The access token lives **10 minutes**; a role change bumps the token version, so old tokens fail at once on every instance. Redis down: authenticated requests get 503 (fail closed), rate limiting degrades to per-instance limits (fail open).
- **Forwarded headers: done.** `X-Forwarded-For` and `X-Forwarded-Proto` are trusted only from `ForwardedHeaders:KnownProxies`; with an empty list the middleware is not added (ADR 0015).
- 683 tests that pass, including a real SQL Server through Testcontainers and architecture rules that fail the build when layering is broken.
- `scripts/verify.sh` and `scripts/verify.ps1`: build with 0 warnings, format check, tests, vulnerable and deprecated package check, naming and secret checks, OpenAPI freshness against the committed `SmartTaskManagement-Backend/docs/openapi.json`, and `docker build`. Both pass locally, also when started from the repository root after the split (ADR 0019); `verify.ps1` was fixed on 2026-11-11 (the PowerShell OpenAPI generator lacked the Redis setting) and passes with Windows PowerShell 5.1. CI runs `verify.sh` on Ubuntu.
- Docker Compose with SQL Server and Redis bound to loopback, and a working multi-stage Dockerfile that runs as a non-root user.
- Claude Code files: the root `CLAUDE.md` (project-wide) and `SmartTaskManagement-Backend/CLAUDE.md` (backend rules), `.claude/rules/`, one `CLAUDE.md` per layer, `architect` and read-only `reviewer` agents, and a whitespace-only format hook.
- ADRs in `SmartTaskManagement-Backend/docs/adr/`, `SmartTaskManagement-Backend/docs/architecture.md`, `SmartTaskManagement-Backend/docs/coding-standards.md`.

### In progress or open

- The AI endpoint (`feat/ai-description`); then the frontend's "Improve with AI" button and the real smoke-test outputs in `PROMPTS.md` section 4.2.
- Frontend follow-ups: Change owner dialog, a project filter on `/tasks`, and seven minor review findings (listed in the PR #7 description).
- `Email:AppBaseUrl` in the backend `appsettings.Development.json` is still `http://localhost:4200`; the HTTPS dev server needs `https://localhost:4200` (a small backend change, waiting for the developer's go-ahead).
- The repository is private: it must be shared with the evaluator (public, or the evaluator added as a collaborator) before submission.

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
| OpenAPI | `SmartTaskManagement-Backend/docs/openapi.json` is committed and checked for freshness in CI; no Spectral | Doubles as the Swagger export deliverable |
| Auth | Implemented in-house (built): 10-minute JWT access token with a token version, rotating refresh token in an HttpOnly cookie. Keycloak was considered and not chosen | The assignment lists these features as deliverables |
| Redis | Allowed only for token versions and the shared rate-limit counter (ADR 0013); any other use needs a new ADR | Revocation and rate limits must agree across instances |

### Banned

AutoMapper (version 14.0.0 has advisory GHSA-rvv3-g6hj-g44x; fixed versions are commercial), MediatR, FluentAssertions, Moq, CSharpier, the Specification pattern, caching (the only Redis uses are in ADR 0013), API versioning, Domain Events, Aspire, StyleCop, Meziantou, AsyncFixer, VS.Threading, commitlint, Renovate, Spectral, SonarQube, NuGet lock files, coverage packages with unclear licenses, any EF Core reference in Domain or Application.

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
10. **Features follow the Sample slice.** Copy the structure in `SmartTaskManagement-Backend/docs/sample-flow.md` and keep the architecture tests green. When controllers or DTOs change, regenerate `SmartTaskManagement-Backend/docs/openapi.json`.
11. **One PRD.** Keep exactly one project PRD, `docs/PRD.md`. After the developer approves it, any change goes into its change log with a date, what and why, and needs approval.

### Working with the developer

The developer reads Bangla and prefers explanations in simple Bangla with English technical terms. When they say they do not understand, answer shortly and simply with an example. Ask one clear question at a time and propose a default. They run commands themselves and paste the output back, so give exact commands and say which terminal to use (Git Bash for `.sh` scripts; in PowerShell a plain `bash` may resolve to WSL and fail; the PowerShell scripts were tested with Windows PowerShell 5.1 only). A sign-in window of the Git credential manager may hide behind other windows during clone or push.

## What to do next

Auth, Project, Task, Dashboard and Notifications are built. Everything below that is not about them is **proposed** until the developer confirms it in the PRD interview.

### First task: the project PRD

A draft exists (`docs/PRD.md`, status Draft); finish it and get it approved. Write `docs/PRD.md` by interviewing the developer, one question at a time, each with a recommended default. Order of topics: roles and permissions, task status rules, project delete behaviour, the AI provider, the frontend location, where the refresh token lives. Mark every requirement CONFIRMED or PROPOSED, and anything about the AI provider that cannot be verified from official documentation as UNVERIFIED. The PRD is final only when the developer approves it and its open-questions list is empty.

### Build order

1. ~~Auth (users, roles, register, login, refresh, logout)~~ built
2. ~~Project module~~ built
3. ~~Task module~~ built
4. ~~Dashboard~~ built (the developer decided on 2026-11-11 to keep the Sample slice for now)
5. ~~Notifications module (live push, stored notification list, email on assignment)~~ built
6. AI endpoint (in progress on `feat/ai-description`)
7. ~~Angular frontend~~ built (PR #7)
8. ~~README, `PROMPTS.md`, API collection export~~ written (`PROMPTS.md` with real smoke-test outputs; the API collection is `SmartTaskManagement-Backend/docs/openapi.json`)

### Auth design

Built as designed in `SmartTaskManagement-Backend/docs/auth-design.md`; the implementation amendments are in ADR 0014 and ADR 0015. The template pieces it uses: `ISecurityContext`, the security-context middleware, `IPasswordHasher`, JWT options validation (signing key of at least 32 characters) and rate limiting.

### Domain rules

Built (decided, implemented and tested): separate aggregates, task status, project soft delete, permissions and the dashboard numbers (details in `SmartTaskManagement-Backend/docs/project-design.md`, `SmartTaskManagement-Backend/docs/task-design.md`, `SmartTaskManagement-Backend/docs/dashboard-design.md` and `SmartTaskManagement-Backend/docs/notifications-design.md`). Still **proposed** (not built): the AI endpoint.

- Project and Task are separate aggregates; a Task references its project only by `ProjectId`.
- Task status: any status to any status (reopen allowed); there is no state machine because there are no transition rules (developer decision 2026-11-10, evidence in `SmartTaskManagement-Backend/docs/task-design.md` section 9).
- Deleting a project is a soft delete and hides its tasks.
- Permissions: Admin sees and manages everything; a Project Manager manages their own projects and tasks and can assign; a Team Member sees only tasks assigned to them (and their projects) and may change only the status of those tasks.
- Dashboard numbers use database-level grouping and are scoped by role (Admin all, Project Manager own projects plus tasks assigned to them (same rule as the lists), Team Member their assigned tasks and those tasks' projects).
- AI endpoint: behind an `IAiProvider` strategy; usable by Admin and Project Manager; input length limit, timeout, clear failure response, and prompt-injection caution. Prompt design is documented in `PROMPTS.md`.

### Frontend (built)

Angular 22, standalone components, zoneless with signals and OnPush, lazy-loaded feature routes, auth and role guards, interceptors (bearer token, single-flight refresh on 401, ProblemDetails mapping), typed reactive forms, responsive layout. Location: `SmartTaskManagement-Frontend/` (PRD section 11, ADR 0019). Design and decisions: `SmartTaskManagement-Frontend/docs/frontend-design.md`.

## Open questions and UNVERIFIED items

| Question | Proposed default |
| --- | --- |
| Is the Angular app in the same repository? | Decided: yes, in `SmartTaskManagement-Frontend/` (PRD section 11, ADR 0019), after the backend is stable |
| Where does Angular keep the refresh token (memory, localStorage, httpOnly cookie)? | Decided and built (PRD 5): access token in memory only, refresh token only in the HttpOnly cookie |
| Are the proposed domain rules and permissions correct? | Confirm one by one in the PRD interview |
| Which GitHub Models model and endpoint will the AI feature use? | Unknown; verify from official documentation |
| Must the repository be public or shared with the evaluator? | The assignment asks for a "GitHub repository link shared with the submission": share it (public, or the evaluator as collaborator) before submitting |

UNVERIFIED:

- The `.ps1` scripts under PowerShell 7 (tested only with Windows PowerShell 5.1), and Linux or macOS support for the scripts.
- Whether `dotnet package list` reports an unreachable vulnerability feed as a problem; restore-time NuGet audit is the second guard.
- The GitHub Models API shape, authentication and rate limits for the AI feature.
- Whether an evaluator accepts in-house Auth as expected; the developer may ask the assignment contact if unsure.

Template follow-ups (not blocking): GitHub Actions are pinned by major version, not by commit SHA; `ubuntu-latest` moves to new Ubuntu releases and may change behaviour; behind a reverse proxy, list its address in `ForwardedHeaders:KnownProxies` before production, otherwise rate limiting sees the proxy address.

## Repo map, commands and verification

### Layout

The repository has two parts plus a thin root (ADR 0019). Tooling never writes the project name: CI and the scripts find the backend as the `*-Backend` folder or the folder that holds the `*.slnx`.

```text
.github/workflows/backend.yml   backend CI (runs for every change except frontend-only ones)
.github/workflows/frontend.yml  frontend CI (runs only for frontend changes)
.claude/                        rules, agents, settings and the format hook (shared by both parts)
README.md  CLAUDE.md            short root versions: overview and project-wide rules
docs/                           project-wide docs: assignment.md, PRD.md, CONTEXT.md (this file)
SmartTaskManagement-Backend/
  Datavanced.SmartTaskManagement.slnx
  src/Domain  src/Application  src/Infrastructure  src/Api
  tests/UnitTests  tests/IntegrationTests  tests/ArchitectureTests  tests/WeavingProbe
  docs/          architecture, coding standards, sample-flow, *-design.md, ADRs, plans, openapi.json
  scripts/       verify.sh, verify.ps1, check-packages, generate-openapi, new-project
  Dockerfile  docker-compose.yml  .env.example  (.env, git-ignored, lives here)
  README.md  CLAUDE.md   backend README and rules; each src project has its own CLAUDE.md
  PROMPTS.md     AI prompt design
SmartTaskManagement-Frontend/
  src/app/core  src/app/shared  src/app/features   Angular app
  e2e/           Playwright end-to-end tests (local only)
  docs/          frontend design, approved screens, plan
  scripts/       verify.sh (lint, format, type-check, tests, build, audit)
  README.md  CLAUDE.md   frontend README and rules
```

Paths such as `src/...`, `tests/...` and `scripts/...` elsewhere in this file are relative to `SmartTaskManagement-Backend/`.

Assemblies and namespaces are `Datavanced.SmartTaskManagement.<Layer>`, set once in `Directory.Build.props`. Package versions live only in `Directory.Packages.props`.

### Commands

Run these from the backend folder (`cd SmartTaskManagement-Backend`) unless noted.

| Task | Command |
| --- | --- |
| Full verification (Git Bash) | `bash scripts/verify.sh` (from the repository root: `bash SmartTaskManagement-Backend/scripts/verify.sh`) |
| Full verification (Windows PowerShell 5.1) | `powershell -File scripts/verify.ps1` |
| Start the database, Redis and Mailpit | `docker compose up -d db redis mailpit` (needs a local `.env` in the backend folder, copied from `.env.example`; Mailpit web UI at `http://localhost:8025`) |
| Local secrets | Set `Database:Name`, `Database:Password`, `Jwt:SigningKey`, `Redis:ConnectionString` and `AdminSeed:Email`, `AdminSeed:Password`, `AdminSeed:FullName` with `dotnet user-secrets set` on `src/Api`; the exact one-liner is in the backend README |
| Run the API | `dotnet run --project src/Api --launch-profile https`, then open `https://localhost:7254/swagger` (Development only; the `http` profile uses port 5199) |
| Regenerate the OpenAPI file | `bash scripts/generate-openapi.sh` and commit `docs/openapi.json` (the backend one) |
| Full stack in Docker | `docker compose up -d` (local only; the compose file runs in Development mode; the API port comes from `API_PORT` in `.env`) |

Docker Desktop must be running for the integration tests and `docker build`.

### Definition of done for any task

The build has 0 warnings, all tests pass, `dotnet format --verify-no-changes` is clean, `scripts/verify` passes after the commit, architecture tests stay green, and `SmartTaskManagement-Backend/docs/openapi.json` is regenerated when the API surface changed. The agent shows the real output of these commands.
