# PRD: Smart Task Management System

- Status: **Draft**, not yet approved. Final only when the developer approves it and
  the open-questions list is empty.
- Updated: 2026-11-09
- Sources: `docs/assignment.md` (the brief), the PRD interview with the developer,
  `docs/adr/` (decisions). Design and implementation details live in design
  documents under `docs/`, which reference this PRD instead of repeating it.

Labels:

- **CONFIRMED**: the developer confirmed it in the interview, or the assignment
  requires it word for word.
- **PROPOSED**: a default from `docs/CONTEXT.md` or the agent, not yet confirmed.
- **UNVERIFIED**: depends on external facts not yet checked in official docs.

## 1. Purpose

A hiring assignment: a secure, maintainable task management system with an
ASP.NET Core 10 API and an Angular frontend. Code quality, security and
documentation matter more than the number of features (assignment, "Final
Goal"). The full functional list is in `docs/assignment.md`; this PRD records the
decisions that the brief leaves open.

## 2. Roles and permissions (CONFIRMED)

Three roles: Admin, Project Manager (PM), Team Member (TM). Each user has exactly
one role.

| Capability | Admin | Project Manager | Team Member |
| --- | --- | --- | --- |
| See projects | All | Only own projects | Only projects where they have at least one assigned task (details only) |
| Create project | Yes | Yes (becomes owner) | No |
| Update / delete project | Any project; ownership does not change | Own projects | No |
| See tasks | All | All tasks in own projects | Only tasks assigned to them |
| Create / update / delete task | Any | In own projects | No |
| Assign task | To anyone | To Team Members or themselves; not to other PMs or Admins | No |
| Search assignable users (by name or email; result shows only name and email) | Yes | Yes: **all** Team Members in the system, not only those already in own projects (a new TM has no tasks yet) | No |
| Change task status | Any | In own projects | Only on tasks assigned to them; status is the only field they may change |
| Use the AI description endpoint | Yes | Yes | No |
| List users (search, paging) and change a user's role | Yes | No | No |
| Create a user directly | No (users only self-register) | No | No |
| Dashboard | Whole system | Own projects | Own assigned tasks |

## 3. Visibility rules (CONFIRMED)

- A resource the caller may not see returns **404 Not Found**, never 403, so its
  existence is not revealed. Applies to another PM's project and to a task not
  assigned to the TM.
- A TM who opens one of "their" projects sees the project details and only their
  own tasks in it, not teammates' tasks.
- When a task is unassigned or reassigned, the previous TM loses access to it
  **immediately**, and to its project if they have no other task there.
  Visibility is therefore decided from current assignments on every request,
  never from data stored in the token.
- Dashboard numbers are scoped exactly like the lists above.

## 4. Registration and the first Admin (CONFIRMED)

- Self-registration always creates a **Team Member**. Only an Admin can change a
  role to Project Manager or Admin.
- The first Admin is **seeded at startup** from secret configuration
  (user-secrets / environment variables), only if no Admin exists yet. Nothing is
  hardcoded and nothing is in the repository. If the settings are missing,
  seeding is skipped.
- Seeding must be safe when several instances start at the same time (see 7).

## 5. Tokens and sessions

### CONFIRMED

- Access token: JWT, lifetime **10 minutes**.
- Every user has a `TokenVersion` in the database; the access token carries it as
  a claim. Every authenticated request compares the token's version with the
  current version held in Redis; on mismatch, **401**. If Redis has no entry, the
  version is loaded from the database once and stored in Redis. There is no
  database query per request for this check. Details: ADR 0013.
- `TokenVersion` increments **only** on role change. User deactivation and
  password change are **out of scope for the first version** (the assignment does
  not require them); if added later, they also increment `TokenVersion`. Each
  increment also revokes all of the
  user's refresh tokens, so a role change applies immediately on all devices and
  instances.
- Redis versions only move forward (compare-and-set); each Redis entry expires a
  fixed time (default 1 hour) after it is written, and a read never extends it. A
  role change writes the new version to Redis first and saves the database only
  after that; if Redis is unavailable the role change returns 503 and nothing is
  saved (ADR 0014).
- **Logout is per device:** it revokes only the current refresh token.
  `TokenVersion` does not change; other devices stay signed in. The logged-out
  device's access token expires on its own within 10 minutes.
- Refresh token: random value from a cryptographic generator, lifetime
  **7 days** (in Options); stored in the database only as a hash.
- Rotation: every refresh revokes the used refresh token and issues a new pair.
- Reuse detection: presenting an already revoked refresh token revokes all
  refresh tokens in that token's family (the chain started by one login); other
  devices stay signed in (ADR 0016).
- Grace period: a token revoked **by rotation less than 30 seconds ago** is not
  treated as reuse (several browser tabs refreshing at once). The request gets
  401 with nothing revoked and no new tokens; the client retries with the new
  cookie. After 30 seconds, normal reuse detection applies.

- Token storage in the browser:
  - The refresh token lives **only** in an `HttpOnly`, `Secure`,
    `SameSite=Strict` cookie whose `Path` covers only the refresh and logout
    endpoints (logout needs the cookie to revoke the token). It is
    never placed in localStorage, sessionStorage or a response body that
    JavaScript stores.
  - The access token lives **only in memory** in the Angular app (no cookie, no
    storage). After a page reload the app gets a new one through the refresh
    cookie.
  - CORS allows credentials only for the explicitly configured frontend origins.

### PROPOSED

- CSRF check on the cookie endpoints (refresh, logout): besides
  `SameSite=Strict`, reject requests whose `Origin` header is not one of the
  configured frontend origins.
- Login returns the same generic error for an unknown email and a wrong password.
- Claims: `sub`, role, token version; role is taken only from the validated
  token, never from a request body.

## 6. Redis and shared state (CONFIRMED, ADR 0013)

- Redis is required because the API runs as **multiple instances**. It is an
  exception to the no-caching rule, limited to token versions and the shared rate
  limit counter.
- Client: StackExchange.Redis 3.4.31 (verified). Image: `redis:8.10.3` (verified on
  Docker Hub), pinned once in `.env.example`. Redis 8 license (RSALv2 / SSPLv1 /
  AGPLv3) accepted: used unmodified as a separate service.
- **Redis unreachable during the version check: fail closed**, authenticated
  requests get **503**. Login, register, refresh and `/health` keep working.
- **Rate limiting:** the per-IP fixed-window limit is shared across instances
  through a hand-written Redis counter (increment plus expiry, no extra package).
  **Redis down: fail open**, each instance falls back to its own in-memory limit.
- **Health:** `/health` includes a hand-written Redis PING check.

## 7. Multi-instance deployment (CONFIRMED)

- **Reverse proxy:** the instances run behind a reverse proxy that sets
  `X-Forwarded-For`. The API uses ASP.NET Core forwarded headers with an explicit
  **trusted-proxy list from configuration**, empty by default (meaning direct
  connections). The header is never trusted from unknown addresses. Rate limiting
  and logs use the resulting client address.
- **Admin seeding race:** two instances starting together must not create two
  Admins (unique email constraint; the duplicate insert is handled).
- **Migrations:** the existing development-only migration on startup stays
  development-only; outside Development, migrations run once as a separate step.
- **Refresh token race:** two instances receiving the same refresh token at the
  same time must not both succeed (optimistic concurrency on the token row).
- **Logs:** every log entry carries an instance identifier.

## 8. Projects and tasks (PROPOSED unless marked)

- CONFIRMED (assignment): project create, update, delete, view, list, search, sort,
  paginate; task create, update, delete, assign, status, priority, due date,
  search, sort, paginate. Statuses: To Do, In Progress, Completed, Cancelled.
  Priorities: Low, Medium, High, Critical.
- PROPOSED: Project and Task are separate aggregates; a task references its
  project by `ProjectId`.
- PROPOSED: status transitions: To Do -> In Progress or Cancelled; In Progress ->
  Completed, Cancelled or To Do; Completed and Cancelled are final. An invalid
  transition returns 409.
- PROPOSED: deleting a project is a soft delete and hides its tasks.

## 9. Dashboard

- CONFIRMED (assignment): total projects, total tasks, tasks by status, tasks by
  priority, completed versus pending, upcoming due tasks.
- CONFIRMED: scoped by role (section 2).
- PROPOSED: numbers computed with database-level grouping; "upcoming" window in
  Options.

## 10. AI description improvement

- CONFIRMED (assignment): one endpoint that fixes grammar, improves clarity, makes
  the text professional, expands short text and makes it actionable.
- CONFIRMED: usable by Admin and PM only.
- PROPOSED: behind an `IAiProvider` strategy; input length limit, timeout, clear
  failure response, prompt-injection caution; prompt design documented in
  `PROMPTS.md`.
- UNVERIFIED: GitHub Models model, endpoint, authentication and rate limits.

## 11. Frontend

- CONFIRMED: the Angular app lives in **this repository**, in the `FE/` folder.
- PROPOSED: Angular 18 or later, built after the backend is stable: standalone components, lazy-loaded routes, auth and role guards,
interceptors (attach token, refresh on 401, error handling), reactive forms,
responsive layout.

## 12. Non-functional (CONFIRMED, assignment)

HTTPS, CORS, input validation (FluentValidation), password hashing, basic rate
limiting, secure configuration, async/await, Swagger, global exception handling,
Serilog, health checks, consistent API responses. Documentation: `README.md`,
`PROMPTS.md`, API collection (the committed `docs/openapi.json`), EF Core
migrations.

## Open questions

| # | Question | Proposed default |
| --- | --- | --- |
| 4 | Task status transitions (section 8) | As proposed |
| 5 | Project delete behaviour (section 8) | Soft delete, tasks hidden |
| 6 | AI provider and model (section 10) | GitHub Models, after verifying official docs |

## Change log

| Date | Change | Why |
| --- | --- | --- |
| 2026-11-09 | First draft from the PRD interview (roles, visibility, registration, seeding, tokens, Redis, multi-instance) | CONTEXT.md: write the PRD first |
| 2026-11-09 | Refresh token (7 days, hash only, rotation, reuse detection) moved to CONFIRMED; open question 1 closed | Developer confirmed |
| 2026-11-09 | User deactivation and password change out of scope for the first version; open question 2 closed | Developer confirmed; not required by the assignment |
| 2026-11-09 | Admin cannot create users; Admin lists users (search, paging) and changes roles; open question 3 closed | Developer confirmed; passwords never pass through an Admin |
| 2026-11-09 | PM searches all Team Members by name or email when assigning (name and email only); open question 9 closed | Developer confirmed; a new TM is in no project yet |
| 2026-11-09 | Refresh token only in an HttpOnly cookie (Path = refresh endpoint), access token only in memory; open question 8 closed | Developer confirmed; a copy in JavaScript-readable storage would defeat HttpOnly |
| 2026-11-09 | Frontend in this repository, folder `FE/`; open question 7 closed | Developer confirmed |
| 2026-11-09 | 30-second grace period for rotated refresh tokens (401, nothing revoked, client retries) | Developer confirmed; several tabs refreshing at once must not sign the user out everywhere |
| 2026-11-10 | Section 5 aligned with ADR 0014: fixed Redis key expiry instead of sliding; role change writes Redis first, then the database (503 and nothing saved when Redis is down) instead of deleting the key after a failed Redis update | ADR 0014 amended ADR 0013 during the design; PRD 5 still described the old behaviour |
| 2026-11-10 | Reuse detection revokes only the presented token's family (one login's chain) instead of all of the user's tokens (ADR 0016) | Developer decision: after a role change the user's devices signed each other out in a loop, and anyone holding an old revoked token could sign the user out everywhere |
