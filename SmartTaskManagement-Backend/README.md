# Smart Task Management System: backend API

The ASP.NET Core (.NET 10) API of the Smart Task Management System: projects,
tasks, a role-scoped dashboard, notifications with live events and email, built
with Clean Architecture, DDD and CQRS Lite. Every rule is enforced by code:
build settings, architecture tests and one verify script (`scripts/verify.sh`),
which is also the backend CI step.

This is the backend folder of the repository (the root `../README.md` gives the
overview; the Angular frontend lives in `../SmartTaskManagement-Frontend/`).
Run every command in this README from this folder:

```bash
cd SmartTaskManagement-Backend
```

`scripts/verify.sh` also works from anywhere
(`bash SmartTaskManagement-Backend/scripts/verify.sh` from the repository root).

## What is inside

- **Layers:** `src/Domain`, `src/Application`, `src/Infrastructure`, `src/Api`
  (dependency rule enforced by architecture tests).
- **Patterns:** DDD building blocks, CQRS Lite with own handler interfaces and a
  Logging -> Validation pipeline, write repositories + `IUnitOfWork`, read
  repositories returning DTOs, `Result` for expected failures, ProblemDetails with
  `errorCode` for every error.
- **Persistence:** EF Core + SQL Server, soft delete, audit fields from
  `TimeProvider`, `RowVersion` concurrency (stale writes return 409).
- **Authentication:** register, login, refresh (rotating refresh token in an
  HttpOnly cookie, reuse detection), logout, `GET /api/auth/me`, roles Admin /
  Project Manager / Team Member, Admin-only user management under `/api/users`,
  first Admin seeded from secret configuration. Design: `docs/auth-design.md`.
- **Redis (shared security state):** access tokens live 10 minutes and carry a
  token version; a role change takes effect on every instance at once (Redis, SQL
  Server is the source of truth). The per-IP rate limit is shared through Redis.
  Redis down: authenticated requests get 503 (fail closed), rate limiting falls
  back to per-instance limits (fail open). ADR 0013, 0014, 0015.
- **Security baseline:** JWT bearer, security context set once by middleware,
  HTTPS/HSTS, security headers, CORS, rate limiting and trusted forwarded headers
  from validated Options, secrets only in user-secrets or environment variables.
- **Quality gates:** warnings as errors with all analyzers, NullGuard runtime null
  checks, `dotnet format`, vulnerable/deprecated package check, committed
  OpenAPI document checked for freshness, Docker image build.
- **Projects:** create, update, soft delete, details with task counts, list with
  search, owner filter, sorting and paging; owner change by an Admin. Design:
  `docs/project-design.md`.
- **Tasks:** create, update, soft delete, assign or unassign, any-to-any status
  change, priority and date-only due date; lists with search, filters, sorting
  and paging. Design: `docs/task-design.md`.
- **Dashboard:** totals, tasks by status and priority, completed versus pending,
  overdue, upcoming due tasks, scoped to what each role may see. Design:
  `docs/dashboard-design.md`.
- **Notifications:** stored notifications, live Server-Sent Events through
  Redis pub/sub, and email through a transactional outbox. Design:
  `docs/notifications-design.md`.
- **Sample feature:** five endpoints under `/api/samples` (all `[Authorize]`),
  kept from the template this project started from as the reference slice for
  new features ([docs/sample-flow.md](docs/sample-flow.md)).

## Backend layout

Paths are relative to this folder; the repository root holds only CI
(`../.github/workflows/backend.yml`), Claude Code tooling (`../.claude/`), git
files and the project-wide docs (`../docs/`: assignment, PRD, CONTEXT). See ADR
0019.

| Path | Content |
| --- | --- |
| `src/` | The four layers |
| `tests/UnitTests` | Domain and Application tests |
| `tests/IntegrationTests` | Real host + SQL Server (Testcontainers), smoke test |
| `tests/ArchitectureTests` | Layer, naming, DI-style and API-surface rules |
| `tests/WeavingProbe` | Woven helper library for the NullGuard behaviour tests |
| `scripts/` | `verify`, `generate-openapi`, `check-packages`, `new-project` (`.sh` and `.ps1`) |
| `docs/` | Architecture, coding standards, ADRs, Sample flow, `openapi.json` |
| `docker-compose.yml`, `.env.example` | Local SQL Server, Redis, Mailpit and Api containers (`.env` lives in this folder) |
| `../.claude/` | Claude Code rules, agents and settings (repository root) |

## Requirements

- .NET SDK 10.0.401 (pinned in `global.json`)
- Docker (SQL Server, Redis and Mailpit for local runs; SQL Server and Redis containers for
  the integration tests)

## Run locally

1. `cp .env.example .env`
2. `docker compose up -d db redis mailpit`
3. Copy the secrets into user-secrets once (Git Bash, backend folder; reads `.env`
   line by line because `ADMIN_SEED_FULL_NAME` contains a space, so the file
   cannot be `source`d):

   ```bash
   g() { grep "^$1=" .env | cut -d= -f2- | tr -d '\r'; }; for kv in "Database:Name=$(g DB_NAME)" "Database:Password=$(g DB_PASSWORD)" "Jwt:SigningKey=$(g JWT_SIGNING_KEY)" "Redis:ConnectionString=127.0.0.1:$(g REDIS_PORT),password=$(g REDIS_PASSWORD)" "AdminSeed:Email=$(g ADMIN_SEED_EMAIL)" "AdminSeed:Password=$(g ADMIN_SEED_PASSWORD)" "AdminSeed:FullName=$(g ADMIN_SEED_FULL_NAME)"; do dotnet user-secrets set "${kv%%=*}" "${kv#*=}" --project src/Api; done
   ```

   If a key is missing from an older `.env`, copy only that key from
   `.env.example` into `.env`; do not overwrite the whole file (an existing
   database volume keeps its old `DB_PASSWORD`). An empty value means the key is
   missing.

   PowerShell:

   ```powershell
   $e=@{}; Get-Content .env | ? { $_ -match '^\w+=' } | % { $k,$v = $_ -split '=',2; $e[$k]=$v }; dotnet user-secrets set Database:Name $e.DB_NAME --project src/Api; dotnet user-secrets set Database:Password $e.DB_PASSWORD --project src/Api; dotnet user-secrets set Jwt:SigningKey $e.JWT_SIGNING_KEY --project src/Api; dotnet user-secrets set Redis:ConnectionString "127.0.0.1:$($e.REDIS_PORT),password=$($e.REDIS_PASSWORD)" --project src/Api; dotnet user-secrets set AdminSeed:Email $e.ADMIN_SEED_EMAIL --project src/Api; dotnet user-secrets set AdminSeed:Password $e.ADMIN_SEED_PASSWORD --project src/Api; dotnet user-secrets set AdminSeed:FullName $e.ADMIN_SEED_FULL_NAME --project src/Api
   ```

   Environment-variable equivalents: `Database__Name`, `Database__Password`,
   `Jwt__SigningKey`, `Redis__ConnectionString`, `AdminSeed__Email`,
   `AdminSeed__Password`, `AdminSeed__FullName`. If you change `DB_PORT`, also set
   `Database:Port`.
4. `dotnet run --project src/Api --launch-profile https` (or `http` if the ASP.NET
   Core development certificate is not trusted; run `dotnet dev-certs https
   --trust` once to trust it). In Development the initial migration applies
   automatically, then the first Admin is seeded.
5. Swagger UI (Development only): `https://localhost:7254/swagger` (`http`
   profile: `http://localhost:5199/swagger`). `POST /api/auth/register` (or
   `login`), copy the `accessToken`, click "Authorize", then call
   `GET /api/samples` (200; without a token 401). `GET /health` checks the
   database and Redis.

Full walkthrough and request paths: [docs/sample-flow.md](docs/sample-flow.md).

### API overview

All routes are under `/api`. Full request and response shapes: `docs/openapi.json`
(or Swagger UI in Development).

| Area | Endpoint | Who |
| --- | --- | --- |
| Auth | `POST /auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout` | Anonymous |
| Auth | `GET /auth/me` | Any signed-in user |
| Users | `GET /users`, `PUT /users/{id}/role` | Admin |
| Users | `GET /users/assignable` | Admin, Project Manager |
| Projects | `GET /projects`, `GET /projects/{id}` | Signed-in user (Admin all, Project Manager own; any assignee also sees the projects of their non-deleted tasks, in the list and in details, read-only) |
| Projects | `POST /projects`, `PUT /projects/{id}`, `DELETE /projects/{id}` | Admin, Project Manager (own) |
| Projects | `PUT /projects/{id}/owner` | Admin |
| Tasks | `POST /projects/{projectId}/tasks`, `PUT /tasks/{id}`, `DELETE /tasks/{id}` | Admin, Project Manager (own project) |
| Tasks | `GET /projects/{projectId}/tasks`, `GET /tasks`, `GET /tasks/{id}` | Signed-in user (Admin all, Project Manager tasks of own projects plus tasks assigned to them, others only tasks assigned to them) |
| Tasks | `PUT /tasks/{id}/status` | Admin, owning Project Manager, or the assignee |
| Tasks | `PUT /tasks/{id}/assignee` | Admin, owning Project Manager (Admin may assign any user, a Project Manager only a Team Member or themselves; null unassigns) |
| Dashboard | `GET /dashboard/summary` (counts: projects, tasks, by status, by priority, completed, pending, overdue) | Signed-in user (sees only what their role may see) |
| Dashboard | `GET /dashboard/upcoming` (pending tasks due in the next days, earliest first) | Signed-in user (sees only what their role may see) |
| Notifications | `GET /notifications` (newest first), `GET /notifications/unread-count`, `POST /notifications/{id}/read`, `POST /notifications/read-all` | Signed-in user (own notifications only) |
| Notifications | `GET /notifications/stream` (live events over SSE) | Signed-in user |
| Samples | `/samples` (copy-source feature) | Signed-in user |

### Live events (SSE)

`GET /api/notifications/stream` sends Server-Sent Events. It needs the
`Authorization: Bearer` header, which the browser `EventSource` cannot send, so
browsers must read it with `fetch` (the token is never put in the URL). Event
types: `notification` (data: the notification), `notifications-read`,
`dashboard-changed` and `heartbeat` (empty; the client ignores it). The stream
ends when the access token expires (at most `Jwt:AccessTokenMinutes`); a role
change or an unavailable token store ends it at the next heartbeat. The client
reconnects with a fresh token. A **reverse proxy must not buffer
`text/event-stream`** responses (for example `proxy_buffering off` in nginx),
or events arrive late.

### Email and Mailpit

Every notification also queues an email, sent by a background worker. Locally
the emails go to Mailpit:

- Start it with `docker compose up -d mailpit` (the web UI is
  `http://localhost:8025`, SMTP is on port 1025).
- If you already have a `.env`, copy `MAILPIT_IMAGE`, `MAIL_SMTP_PORT` and
  `MAIL_UI_PORT` from `.env.example` into it.
- `appsettings.Development.json` points `dotnet run` at `localhost:1025` without
  TLS; the Docker Compose api container uses `Email__Host=mailpit`.
- `MAIL_SMTP_PORT` in `.env` only changes the host port Docker Compose publishes.
  If you change it, also set `Email__Port` to the same value for `dotnet run`
  (`dotnet user-secrets set Email:Port <port> --project src/Api` or the
  `Email__Port` environment variable); `appsettings.Development.json` keeps 1025.

Settings (section `Email`, validated at startup): `Host`, `Port`, `UseTls`
(STARTTLS), `UserName`, `Password`, `FromAddress`, `FromName`, `AppBaseUrl`,
`TaskPathTemplate`, `ProjectPathTemplate` (must contain `{id}`), `PollSeconds`,
`BatchSize`, `ClaimSeconds`, `MaxAttempts`. To use a real SMTP provider, set
`Email__Host`, `Email__Port`, `Email__UseTls`, `Email__UserName` and
`Email__Password` as environment variables (and `Email__FromAddress`,
`Email__AppBaseUrl` for your sender and web app). The password lives only in
user-secrets or environment variables. `UseTls=false` is allowed only in
Development and Testing.

### Authentication and the first Admin

- Register creates a Team Member. Only an Admin can change a role
  (`PUT /api/users/{id}/role`); a role change signs the user out everywhere.
- **First Admin.** `AdminSeed:Email`, `AdminSeed:Password`, `AdminSeed:FullName`:
  set all three or none (none = no seeding; some = startup fails). At startup,
  if no Admin exists, one is created. Password: 8 to 128 characters with a letter
  and a digit.
- **Seed recovery.** If the seed email already belongs to a non-Admin account,
  startup fails and the account is never promoted. Set a different
  `AdminSeed:Email` and restart.
- **No password change in v1.** The seeded Admin's password can only be changed
  by a future feature; choose it accordingly outside local development.
- The refresh token is only in the `__Secure-refresh_token` cookie (HttpOnly,
  Secure, SameSite=Strict, path `/api/auth`). `refresh` and `logout` check the
  `Origin` header against `Cors:AllowedOrigins`.

### Database migrations outside Development

Only Development migrates automatically. Elsewhere apply an idempotent SQL
script before starting the API (the first-Admin seeding needs the schema and the
startup fails without it):

```bash
Database__Name=design-time Database__Password=design-time dotnet ef migrations script --idempotent --project src/Infrastructure --startup-project src/Api --output migrate.sql
```

## Run the full stack in Docker (local only)

```bash
cp .env.example .env
docker compose up -d --build
curl http://127.0.0.1:5080/health
docker compose down
```

> **`docker-compose.yml` is for local development only, not for production.**
> The api container runs with `ASPNETCORE_ENVIRONMENT=Development` because the
> local SQL Server has no trusted certificate and `Database:Encrypt=false` is
> rejected in every other environment (ADR 0011). All ports bind to `127.0.0.1`,
> and the values come from the dev-only placeholders in `.env`.

## Known limitations

- **Forwarded headers are off until proxies are listed.** `X-Forwarded-For` and
  `X-Forwarded-Proto` are honoured only from the addresses in
  `ForwardedHeaders:KnownProxies` (list of IP addresses, empty by default). With an
  empty list the middleware is not added at all, so direct clients cannot spoof
  their address. **Behind a reverse proxy or load balancer, list its IP address(es)**;
  otherwise rate limiting sees the proxy's IP (every client shares one limit) and
  HTTPS redirection may loop. Startup fails if
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` or `FORWARDEDHEADERS_ENABLED` is true (it
  would trust every sender). `/health` is excluded from rate limiting.
- **The seeded Admin's password cannot be changed in v1** (see "Authentication and
  the first Admin").
- **Logout is per device.** The logged-out device's access token stays valid for
  at most 10 minutes (ADR 0013); an open live stream also ends at that expiry.
- **Redis is required.** When it is down, authenticated requests return 503
  (`auth.unavailable`); register and login still work (a failed token version
  publish is only logged); `/health` reports Redis unhealthy.
- **With `AdminSeed` configured, the host cannot start while SQL Server is
  unreachable**: the seed runs at startup and needs the database. In Docker
  Compose, `restart: on-failure` retries until SQL Server is up; elsewhere the
  orchestrator must restart the process.

## Origin

This repository was created from a Clean Architecture template. The template's
rename scripts (`scripts/new-project.sh` and `.ps1`) and the Sample slice are
kept: the scripts are part of the verified tooling, and Sample is the reference
slice that every feature was copied from (`docs/sample-flow.md`).

## Tests and checks

- `dotnet test --solution Datavanced.SmartTaskManagement.slnx`
- Regenerate the OpenAPI document: `bash scripts/generate-openapi.sh`
- Verify (Git Bash, Linux, CI): `bash scripts/verify.sh`
- Verify (Windows): `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`

> **Tested PowerShell:** the `.ps1` scripts were run and passed with
> **Windows PowerShell 5.1** (5.1.26100). They were **not** tested with
> PowerShell 7 (`pwsh`), which is not installed on the development machine.
> CI runs only `scripts/verify.sh`.

## Documentation

- [docs/architecture.md](docs/architecture.md), [docs/adr/](docs/adr/README.md)
- [docs/coding-standards.md](docs/coding-standards.md), [docs/sample-flow.md](docs/sample-flow.md)

## Contributing

Use the commit prefixes `feat:`, `fix:` and `docs:`, run the verify script before
pushing, and follow the pull request checklist (`../.github/pull_request_template.md`).
