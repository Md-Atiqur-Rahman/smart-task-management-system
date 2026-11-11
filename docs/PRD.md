# PRD: Smart Task Management System

- Status: **Draft**, not yet approved. Final only when the developer approves it and
  the open-questions list is empty.
- Updated: 2026-11-11
- Sources: `docs/assignment.md` (the brief), the PRD interview with the developer,
  `SmartTaskManagement-Backend/docs/adr/` (decisions). Design and
  implementation details live in design documents under
  `SmartTaskManagement-Backend/docs/` (and later the frontend's `docs/`), which
  reference this PRD instead of repeating it.

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
| See projects | All | Own projects, and projects where they have at least one assigned task (details only) | Only projects where they have at least one assigned task (details only) |
| Create project | Yes; must choose a Project Manager as owner | Yes; always becomes the owner | No |
| Update (name, description) / delete project | Any project | Own projects | No |
| Change a project's owner | Yes; the new owner must be a Project Manager | No | No |
| See tasks | All | All tasks in own projects, and tasks assigned to them | Only tasks assigned to them |
| Create / update / delete task | Any | In own projects | No |
| Assign task | To anyone | To Team Members or themselves; not to other PMs or Admins | No |
| Search assignable users (by name or email; result shows only name and email) | Yes | Yes: **all** Team Members in the system, not only those already in own projects (a new TM has no tasks yet) | No |
| Change task status | Any | In own projects, and on tasks assigned to them | Only on tasks assigned to them; status is the only field they may change |
| Use the AI description endpoint | Yes | Yes | No |
| List users (search, paging) and change a user's role | Yes | No | No |
| Create a user directly | No (users only self-register) | No | No |
| Dashboard | Whole system | Own projects, and tasks assigned to them (same rule as the lists, section 3) | Own assigned tasks |

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
- CONFIRMED: whoever a task is assigned to can see that task and its project
  (details only) and change the task's status, whatever their role. This covers
  a Project Manager or Admin assigned to a task in a project they do not own,
  and a previous project owner who is still assigned to tasks there. On a
  project they do not own they may change only the status of their own tasks.

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
  exception to the no-caching rule, limited to token versions, the shared rate
  limit counter and (CONFIRMED 2026-11-11, ADR 0018) pub/sub fan-out of live
  events to the instance that holds a user's live connection (section 13).
- Client: StackExchange.Redis 3.4.31 (verified). Image: `redis:8.10.3` (verified on
  Docker Hub), pinned once in the backend `.env.example`. Redis 8 license (RSALv2 / SSPLv1 /
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
- CONFIRMED: a project has a name (required, max 200), a description (optional,
  max 2000) and an owner (always a Project Manager); created/updated times are
  kept by the system. No status or deadline.
- CONFIRMED: an Admin creating a project must choose a Project Manager as owner;
  a Project Manager creating a project is always its owner. Only an Admin can
  change the owner later (for example when a PM leaves), and only to another
  Project Manager. The previous owner loses access immediately (section 3).
- CONFIRMED: a Project Manager who owns at least one (not deleted) project cannot
  be demoted; the role change returns 409 until the projects are given to
  another Project Manager.
- CONFIRMED: deleting a project is a soft delete. The project and its tasks
  disappear from lists and search, and direct access returns 404, for everyone
  including Admins. Admin and the owning PM can delete. No restore feature;
  restoring is a database operation.
- CONFIRMED: project list: keyword search on name and description; Admins can
  filter by owner; sort by name, created time or updated time; pagination,
  search, filter and sort are server-side (database), default page size 20,
  maximum 100. A list row shows name, owner name, created and updated time;
  details add the full description and the owner's name and email. Task counts
  come with the Task module.
- CONFIRMED: Project and Task are separate aggregates; a task references its
  project by `ProjectId`.
- CONFIRMED: no status transition rules: a task may move from any status to any
  other status, including reopening a Completed or Cancelled task, like the
  default workflows of Azure Boards, Jira (team-managed) and Zoho. Setting the
  current status again changes nothing. Priority has no transition rules either.
- CONFIRMED: a task has a title (required, max 200), a description (optional,
  max 4000), a status (To Do when created), a priority (Medium when not given), a
  due date (optional, date only without time, past dates allowed) and an assignee
  (optional). Created/updated times are kept
  by the system.
- CONFIRMED: deleting a task is a soft delete, like a project: it disappears from
  lists, search, counts and the dashboard, and direct access returns 404 for
  everyone including Admins. No restore feature.
- CONFIRMED: task lists: one per project and one across all visible projects;
  keyword search on title and description; filters on status, priority, assignee,
  unassigned tasks only, and a due-date range; sort by title, priority, due date, status, created time or
  updated time; server-side paging (default 20, maximum 100). A list row shows
  title, status, priority, due date, assignee name, project name, created and
  updated time; details add the full description, the assignee's email and the
  project id.
- CONFIRMED: separate operations for editing details (title, description,
  priority, due date), changing the status, and assigning or unassigning, so a
  Team Member can only ever reach the status operation.
- CONFIRMED: project task counts: a project list row shows the total and the
  completed number of tasks; project details show the count per status. Counts
  follow visibility: a user who sees a project only through assigned tasks sees
  only the counts of their own tasks. Deleted tasks are not counted.

## 9. Dashboard

- CONFIRMED (assignment): total projects, total tasks, tasks by status, tasks by
  priority, completed versus pending, upcoming due tasks.
- CONFIRMED: scoped by role (section 2), exactly like the task and project lists
  (section 3); deleted tasks and tasks of deleted projects are never counted.
- CONFIRMED: two calls, one for all the numbers and one for the upcoming list,
  so the cards load first and the numbers always add up; numbers are computed in
  the database (grouping and counts), never cached.
- CONFIRMED: completed = Completed; pending = To Do + In Progress; Cancelled is in
  neither. An overdue count (pending tasks due before today) is shown too.
- CONFIRMED: upcoming = pending tasks due from today to today + 7 days, at most
  10, earliest first, then by priority (Critical first); both numbers are
  settings. "Today" is the server's UTC date.
- CONFIRMED: live updates, stored notifications and email on assignment come in a
  separate Notifications module after the dashboard, not in it.

## 10. AI description improvement

- CONFIRMED (assignment): one endpoint that fixes grammar, improves clarity, makes
  the text professional, expands short text and makes it actionable.
- CONFIRMED: usable by Admin and PM only.
- PROPOSED: behind an `IAiProvider` strategy; input length limit, timeout, clear
  failure response, prompt-injection caution; prompt design documented in
  `PROMPTS.md`.
- CONFIRMED (2026-11-11): provider is the **Google Gemini API, free tier**, with an
  API key from Google AI Studio (key only in user-secrets or environment
  variables). Model and API details are verified from Google's official docs in
  the AI design.
- Why not GitHub Models (the provider the assignment suggests): GitHub's official
  docs say "As of July 30, 2026, GitHub Models has been fully retired. The
  playground, model catalog, inference API, and bring your own key (BYOK) are no
  longer available to any customer" (docs.github.com/en/github-models/quickstart,
  checked 2026-11-11); the Marketplace models listing no longer shows models. The
  assignment allows "any free AI provider".
- Considered and not chosen: Ollama (local, free, keeps text on the machine) —
  rejected for now because setting it up (model download, hardware, tuning for
  Bangla/Banglish) would take too long. The `IAiProvider` strategy keeps it
  possible later.
- CONFIRMED: Gemini free-tier terms apply: Google may use submitted text to improve
  its products and human reviewers may read it, so no sensitive or personal
  information belongs in descriptions sent for improvement; users must be 18+ and
  use is professional (documented in `PROMPTS.md`).
- CONFIRMED: output keeps the input's language (Bangla, English or Banglish); the
  result is shown to the PM first and is used only when the PM chooses "Use this"
  (the endpoint saves nothing); on failure or after a 30-second timeout the PM sees a
  clear message and the original text is unchanged; no per-user usage limit for now.

## 11. Frontend

- CONFIRMED: the Angular app lives in **this repository**, in the
  `SmartTaskManagement-Frontend/` folder; the backend lives in
  `SmartTaskManagement-Backend/` (ADR 0019). Each folder has its own README,
  docs, compose and env files; the project-wide docs (assignment, this PRD,
  CONTEXT) stay in the root `docs/`.
- PROPOSED: Angular 18 or later, built after the backend is stable: standalone components, lazy-loaded routes, auth and role guards,
interceptors (attach token, refresh on 401, error handling), reactive forms,
responsive layout.

## 12. Non-functional (CONFIRMED, assignment)

HTTPS, CORS, input validation (FluentValidation), password hashing, basic rate
limiting, secure configuration, async/await, Swagger, global exception handling,
Serilog, health checks, consistent API responses. Documentation: `README.md`,
`PROMPTS.md`, API collection (the committed `SmartTaskManagement-Backend/docs/openapi.json`), EF Core
migrations.

## 13. Notifications

Design: `SmartTaskManagement-Backend/docs/notifications-design.md`.

- CONFIRMED: when a task is assigned, the assignee gets a live pop-up (toast) and
  an email; the notification is stored and shown in a notification list (pop-up
  list under a bell icon). Clicking an item opens the task.
- CONFIRMED: when an Admin creates a project for a Project Manager or changes a
  project's owner, the new owner gets a notification and an email; on an owner
  change the previous owner gets a notification that ownership was removed.
- CONFIRMED: when a task is unassigned or reassigned, the previous assignee gets a
  notification that the task was removed from them.
- CONFIRMED: notifications are stored in the database and deleted automatically
  after **7 days**; the list shows the last 7 days.
- CONFIRMED: the bell icon shows a badge with the unread count; inside the list
  each item shows itself as unread (active) or read (inactive), without counts.
- CONFIRMED: the dashboard updates in real time: when work changes, the open
  dashboards of the affected users refresh without a reload.
- CONFIRMED: live updates use Server-Sent Events (SSE).
- CONFIRMED: an assignment succeeds even when the mail server is down; email
  goes through a transactional outbox in the database and a background worker
  retries failed sends (ADR 0017).
- CONFIRMED: email is sent with MailKit over SMTP; development uses a local
  Mailpit inbox in Docker; a real provider is a configuration change only.
- CONFIRMED: several API instances (section 7): live events reach the right
  instance through Redis pub/sub, and the email worker claims each email so it is
  sent once (ADR 0018, ADR 0017).
- CONFIRMED (design approved 2026-11-11): the person who made the change never gets a notification about it;
  every notification type also sends an email; "removed" notifications do not
  link to the task or project.

## Open questions

None open.

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
| 2026-11-10 | Projects: fields, ownership (Admin chooses a PM owner; only an Admin can change the owner later, replacing "ownership does not change"), soft delete (open question 5 closed), list search/filter/sort/server-side paging, list and details contents, no demotion of a PM who owns projects | Developer confirmed in the Project module interview; a project must be handed to another PM when its PM leaves |
| 2026-11-10 | Tasks: separate aggregate, no status transition rules (any status to any status; replaces the proposed transition table; open question 4 closed), fields, date-only due date with past dates allowed, soft delete, lists with search/filter/sort/paging, list and details contents, separate status and assignment operations, project task counts. Sections 2 and 3: whoever a task is assigned to sees it and its project and may change its status, whatever their role | Developer confirmed in the Task module interview; an Admin may assign anyone, and the assignee must be able to see the work; Azure Boards, Jira and Zoho allow any-to-any status changes by default, so final statuses would block normal reopening |
| 2026-11-11 | Dashboard (section 9): scope like the lists (section 2 PM dashboard row corrected from "own projects"), two calls (summary and upcoming), completed/pending/overdue definitions, upcoming window of 7 days and 10 tasks, no caching; notifications moved to a separate later module | Developer confirmed in the Dashboard interview; one visibility rule keeps dashboard numbers equal to what the lists show |
| 2026-11-11 | Notifications (section 13): five notification events (task assigned, task removed, project owner set by an Admin, owner changed for the new and the previous owner), live pop-up and real-time dashboard over SSE, stored list with 7-day retention, unread badge, email through a database outbox with MailKit and Mailpit; section 6 Redis exception extended to pub/sub fan-out | Developer confirmed in the Notifications interview; several instances need a shared channel, and an assignment must not fail because the mail server is down |
| 2026-11-11 | Repository split into backend and frontend folders (ADR 0019); section 11: the frontend folder is `SmartTaskManagement-Frontend/` (replaces `FE/`), the backend is in `SmartTaskManagement-Backend/` | Developer decision: each part keeps its own docs, compose and env files |
| 2026-11-11 | AI provider (section 10): Google Gemini API free tier instead of the retired GitHub Models; Ollama considered and deferred; output keeps the input language; preview-then-"Use this"; 30-second timeout; no per-user limit; open question 6 closed | Developer confirmed in the AI interview; GitHub's docs state GitHub Models was retired on July 30, 2026 |
