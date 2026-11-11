# Smart Task Management System

A secure, maintainable task management system: an **ASP.NET Core 10 API**
(Clean Architecture, DDD, CQRS Lite) and an **Angular 22 frontend**. Teams plan
projects, create and assign tasks, follow progress on a live dashboard and get
notified when work is assigned to them. Built as a hiring assignment
([brief](docs/assignment.md)), with code quality, security and documentation
first. Decisions the brief leaves open are in the [PRD](docs/PRD.md).

## Features

- **Authentication:** register, login, logout, JWT access tokens (10 minutes),
  rotating refresh tokens in an HttpOnly cookie with reuse detection, password
  hashing.
- **Roles:**
  - Admin manages everything, including user roles.
  - A Project Manager manages their own projects and tasks and assigns work.
  - A Team Member sees the tasks assigned to them and changes their status.
- **Projects:** create, update, delete (soft), view details with task counts,
  list with keyword search, owner filter, sorting and server-side paging.
- **Tasks:**
  - create, update, delete (soft), assign or unassign, change the status
    (To Do, In Progress, Completed, Cancelled; any status to any status);
  - priority (Low, Medium, High, Critical), date-only due date;
  - search, filters (status, priority, assignee, unassigned, due-date range),
    sorting and paging.
- **Dashboard:** total projects and tasks, tasks by status and priority,
  completed versus pending, overdue, upcoming due tasks. It updates live.
- **Notifications:**
  - a bell with an unread badge and a list (mark read, mark all read);
  - live toasts over Server-Sent Events;
  - an email for each notification, sent through a transactional outbox
    (Mailpit locally).
- **AI description improvement:** an endpoint that rewrites a rough task
  description into a clear, actionable one, using the Google Gemini API.
  Prompt design: [PROMPTS.md](SmartTaskManagement-Backend/PROMPTS.md).
- **Responsive, accessible UI** from 360 px phones to desktop.

## Technology stack

| Part | Technology |
| --- | --- |
| Backend | .NET 10, ASP.NET Core Web API, EF Core + SQL Server, FluentValidation, Serilog, Redis (token versions, shared rate limit, live-event fan-out), MailKit, Swagger / OpenAPI 3.1 |
| Frontend | Angular 22 (standalone components, signals, zoneless, OnPush), TypeScript 6 strict, typed reactive forms |
| AI | Google Gemini API (free tier) behind an `IAiTextImprover` interface |
| Tests | xUnit v3, NSubstitute, Shouldly, NetArchTest, Testcontainers (backend); Vitest, Playwright (frontend) |
| Tooling | Docker Compose (SQL Server, Redis, Mailpit), GitHub Actions, `dotnet format`, angular-eslint, Prettier |

## Architecture

**Backend:** Clean Architecture with four layers. Architecture tests fail the
build when the dependency rule is broken.

- **Domain:** aggregates with invariants.
- **Application:** use cases as commands and queries with their own handler
  interfaces (no MediatR), a logging and validation pipeline, and `Result` for
  expected failures.
- **Infrastructure:** EF Core, Redis, email, the Gemini client.
- **Api:** controllers, middleware, composition root.

Writes go through write repositories and `IUnitOfWork`; reads go through read
repositories that return DTOs. Every error is ProblemDetails with an
`errorCode`. Details: [architecture](SmartTaskManagement-Backend/docs/architecture.md),
[ADRs](SmartTaskManagement-Backend/docs/adr/README.md).

**Frontend:**
- `core/`: session, interceptors, guards, error mapping, SSE client, shell.
- `shared/`: domain-free UI.
- `features/`: auth, dashboard, projects, tasks, users. Each feature has pages,
  components, data-access and models, and uses another feature only through
  that feature's public `index.ts`.

All routes are lazy-loaded and guarded. HTTP concerns live in interceptors:
bearer token, single-flight refresh on 401, ProblemDetails mapping.
Details: [frontend design](SmartTaskManagement-Frontend/docs/frontend-design.md).

**Patterns used where they fit:** Clean Architecture, CQRS Lite, repository and
unit of work, `Result`, strategy (`IAiTextImprover`), options with validation on
start, transactional outbox, middleware, interceptor.

## Folder structure

```text
.github/workflows/              CI: backend.yml and frontend.yml run each part's verify script
docs/                           assignment.md, PRD.md, CONTEXT.md (project-wide)
SmartTaskManagement-Backend/
  src/Domain  src/Application  src/Infrastructure  src/Api
  src/Infrastructure/Persistence/Migrations    EF Core migrations
  tests/UnitTests  tests/IntegrationTests  tests/ArchitectureTests
  docs/                         architecture, design docs, ADRs, openapi.json
  scripts/verify.sh             the full backend check (also CI)
  PROMPTS.md                    AI prompt design
  docker-compose.yml  Dockerfile  .env.example
SmartTaskManagement-Frontend/
  src/app/core  src/app/shared  src/app/features
  e2e/                          Playwright end-to-end tests
  docs/                         frontend design, approved screens, plan
  scripts/verify.sh             the full frontend check (also CI)
```

The repository layout is recorded in
[ADR 0019](SmartTaskManagement-Backend/docs/adr/0019-repository-layout.md).

## Setup

**Requirements:**
- .NET SDK 10.0.401 (pinned in `global.json`)
- Node.js 22 (`^22.22.3`, see `.nvmrc`)
- Docker Desktop

**1. Backend** (details: [backend README](SmartTaskManagement-Backend/README.md)):

```bash
cd SmartTaskManagement-Backend
cp .env.example .env                       # dev-only placeholders
docker compose up -d db redis mailpit      # SQL Server, Redis, Mailpit
# copy the secrets into user-secrets once: the one-line command is in the backend README
dotnet run --project src/Api --launch-profile https
```

- The API runs on `https://localhost:7254`.
- Swagger UI (Development only) is at `https://localhost:7254/swagger`.
- In Development the EF Core migrations apply automatically, and the first Admin
  is created from the `AdminSeed:*` secrets.
- Optional, for the AI endpoint: `dotnet user-secrets set "Ai:Gemini:ApiKey"
  "<your-key>" --project src/Api` (a free key from Google AI Studio). Without it,
  everything else works and the AI endpoint answers `ai.not_configured`.

**2. Frontend** (details: [frontend README](SmartTaskManagement-Frontend/README.md)):

```bash
# allow the frontend origin (backend folder, once)
dotnet user-secrets set "Cors:AllowedOrigins:0" "https://localhost:4200" --project src/Api

cd ../SmartTaskManagement-Frontend
dotnet dev-certs https --export-path certs/localhost.pem --format Pem --no-password
npm ci
npm start                                  # https://localhost:4200
```

The dev server uses HTTPS because the refresh-token cookie is
`SameSite=Strict` and `Secure`; the frontend and the API must use the same
scheme.

**Database:**
- EF Core migrations are in
  `SmartTaskManagement-Backend/src/Infrastructure/Persistence/Migrations`.
- Outside Development, generate an idempotent SQL script; the command is in the
  backend README, "Database migrations outside Development".

## API overview

All routes are under `/api`. Full contract:
[`SmartTaskManagement-Backend/docs/openapi.json`](SmartTaskManagement-Backend/docs/openapi.json)
(OpenAPI 3.1, also the API collection: import it into Postman or Swagger).

| Area | Endpoints | Who |
| --- | --- | --- |
| Auth | `POST /auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout`; `GET /auth/me` | Anonymous; `me` signed in |
| Users | `GET /users`, `PUT /users/{id}/role` | Admin |
| Users | `GET /users/assignable` | Admin, Project Manager |
| Projects | `GET /projects`, `GET /projects/{id}` | Signed in, scoped by role |
| Projects | `POST /projects`, `PUT /projects/{id}`, `DELETE /projects/{id}` | Admin, owning Project Manager |
| Projects | `PUT /projects/{id}/owner` | Admin |
| Tasks | `GET /projects/{projectId}/tasks`, `GET /tasks`, `GET /tasks/{id}` | Signed in, scoped by role |
| Tasks | `POST /projects/{projectId}/tasks`, `PUT /tasks/{id}`, `DELETE /tasks/{id}`, `PUT /tasks/{id}/assignee` | Admin, owning Project Manager |
| Tasks | `PUT /tasks/{id}/status` | Admin, owning Project Manager, the assignee |
| AI | `POST /tasks/description/improve` | Admin, Project Manager |
| Dashboard | `GET /dashboard/summary`, `GET /dashboard/upcoming` | Signed in, scoped by role |
| Notifications | `GET /notifications`, `GET /notifications/unread-count`, `POST /notifications/{id}/read`, `POST /notifications/read-all`, `GET /notifications/stream` (SSE) | Signed in, own only |
| Health | `GET /health` (database and Redis) | Anonymous |

Responses:
- Success returns the DTO, or `PagedResult<T>` for lists.
- Errors return ProblemDetails with an `errorCode`.
- Status codes: 400 validation, 401, 403, 404 (also for things the caller may
  not see), 409 conflict, 429 rate limit, 503 when Redis or the AI service is
  unavailable.

## Security

- JWT access tokens with a token version; a role change takes effect at once on
  every instance (Redis).
- Rotating refresh tokens:
  - stored only as a hash;
  - kept only in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie;
  - reuse detection per token family;
  - the `Origin` header is checked on refresh and logout.
- The browser keeps the access token in memory only, never in localStorage.
- Passwords are hashed with ASP.NET Core Identity's hasher. Every role rule is
  enforced by the API; the UI only hides what a role cannot do.
- HTTPS with HSTS, security headers, CORS limited to configured origins, and a
  per-IP rate limit (shared through Redis).
- FluentValidation on every request; options validated on start.
- Secrets live only in user-secrets or environment variables, never in the
  repository. Database TLS is never disabled outside Development.

## Testing and quality

| Check | Command | Content |
| --- | --- | --- |
| Backend verify | `bash SmartTaskManagement-Backend/scripts/verify.sh` | Release build with 0 warnings, `dotnet format`, 683 tests (unit, integration with a real SQL Server through Testcontainers, architecture), vulnerable package check, OpenAPI freshness, Docker build |
| Frontend verify | `bash SmartTaskManagement-Frontend/scripts/verify.sh` | angular-eslint, Prettier, strict type-check, 76 unit tests (Vitest), production build, `npm audit` |
| End-to-end | `npm run e2e` (frontend folder, API running) | Playwright on Edge: register, reload keeps the session, logout and login; the Admin flow (promote a Project Manager, create a project and an assigned task, live notification) |

GitHub Actions runs both verify scripts on pull requests and on `main`: the
frontend workflow for frontend changes, the backend workflow for everything
else.

## Assignment deliverables

| Deliverable | Where |
| --- | --- |
| Source code | This repository |
| SQL script or EF Core migrations | `SmartTaskManagement-Backend/src/Infrastructure/Persistence/Migrations` (idempotent SQL script command in the backend README) |
| README.md | This file, plus the [backend](SmartTaskManagement-Backend/README.md) and [frontend](SmartTaskManagement-Frontend/README.md) READMEs |
| PROMPTS.md | [SmartTaskManagement-Backend/PROMPTS.md](SmartTaskManagement-Backend/PROMPTS.md) |
| API collection | [SmartTaskManagement-Backend/docs/openapi.json](SmartTaskManagement-Backend/docs/openapi.json) (OpenAPI 3.1; importable into Postman) |

## More documentation

- Backend: [README](SmartTaskManagement-Backend/README.md),
  [architecture](SmartTaskManagement-Backend/docs/architecture.md),
  [coding standards](SmartTaskManagement-Backend/docs/coding-standards.md),
  design documents and [ADRs](SmartTaskManagement-Backend/docs/adr/README.md) in
  `SmartTaskManagement-Backend/docs/`.
- Frontend: [README](SmartTaskManagement-Frontend/README.md),
  [design](SmartTaskManagement-Frontend/docs/frontend-design.md),
  [approved screens](SmartTaskManagement-Frontend/docs/design/prototype/README.md).
- Project: [assignment](docs/assignment.md), [PRD](docs/PRD.md),
  [current state](docs/CONTEXT.md).
