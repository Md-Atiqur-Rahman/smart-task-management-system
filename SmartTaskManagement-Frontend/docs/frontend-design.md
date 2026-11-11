# Frontend design: Smart Task Management (Angular)

- Status: **Approved design** (screens approved 2026-11-11), written for the
  implementation plan.
- Sources, in order of authority: `../../docs/assignment.md` (the brief),
  `../../docs/PRD.md` (decisions; sections 2, 3, 5, 8, 9, 11, 13), the backend
  contract `../../SmartTaskManagement-Backend/docs/openapi.json`, and the backend
  designs in `../../SmartTaskManagement-Backend/docs/` (`auth-design.md`,
  `project-design.md`, `task-design.md`, `dashboard-design.md`,
  `notifications-design.md`, ADR 0013-0019). This document does not repeat them;
  it records how the frontend uses them and the decisions made for the frontend.
- Approved screens: `design/prototype/` (copy of the design canvas,
  https://claude.ai/artifact/BXE1WVVCVv3FtKtjWxAnBJ). The screens are the visual
  baseline; this document and the API are the requirements.

Labels: **[decided]** = the developer decided it in the frontend design
interview (2026-11-11); **[builder's call]** = left to the implementer, who may
change it with a short note in the plan; **UNVERIFIED** = must be proven by
running it before it is relied on.

## 1. Intent

Build the Angular frontend the brief asks for (login, dashboard, project and
task management, search, responsive UI; lazy loading, route guards, HTTP
interceptors, reactive forms), plus what the backend already offers: role-aware
UI for Admin, Project Manager (PM) and Team Member (TM) exactly as PRD 2-3, user
role management, assignable-user search, notifications with a live toast and
badge, and a live dashboard over SSE. The assignment is judged on code quality,
security and maintainability, so the frontend must be small, strict and
obviously correct rather than feature-rich.

### Goals

- Every screen of `design/prototype/` (v2) works against the real API.
- The API is the authority: the frontend calls only endpoints in `openapi.json`,
  never invents fields, and repeats backend rules only as UX hints (form
  limits, which buttons to show). The backend decides; the UI handles its answer.
- Security exactly as PRD 5 and ADR 0014-0016 (section 6).
- Strict TypeScript, OnPush, signals, typed reactive forms, lazy routes; lint,
  format, type-check, unit tests and a production build pass in one verify script.

### Non-goals and deferred work

- **No backend change** [decided]. If a missing field or endpoint is found,
  stop and propose a separate backend task.
- **AI "Improve with AI"** (built 2026-11-12): in the new-task and edit-task
  dialogs (Admin and PM only; a Team Member never opens them). It calls
  `POST /api/tasks/description/improve` with the title and description (30 s
  client timeout), shows the suggestion, and replaces the description only when
  the user chooses "Use this description". A note says the text is sent to
  Google Gemini. Errors: `ai.rejected` (400) asks the user to rewrite the text;
  `ai.unavailable` and `ai.not_configured` (503) and a timeout keep the user's
  text unchanged.
- **E2E tests (Playwright)**: built on 2026-11-11 at the developer's request (section 13).
- **CI workflow `.github/workflows/frontend.yml`**: built on 2026-11-11 (section 13).
- No i18n, no dark theme, no offline mode, no state library (NgRx etc.), no UI
  component library [builder's call: plain CSS keeps the dependency list short].
- `GET /api/auth/me` is not used: login and refresh already return the user
  (`AuthResponse.user`), so no screen needs it. The `/api/samples` endpoints are
  template examples and are not used.

### Anti-goals (would be a failure even if it works)

- An access token or refresh token in `localStorage`, `sessionStorage`, a
  non-HttpOnly cookie or a URL.
- A button that the user's role can never use (it would end in 403).
- A list or form field that does not exist in the API, or a client-side rule
  that contradicts the API (for example status transition rules).

## 2. Decisions from the design interview (2026-11-11)

| # | Decision | Effect |
| --- | --- | --- |
| 1 | **Option B: no backend change for task permissions.** The task DTOs carry no project owner id. | Edit, assign and delete appear on the project detail page (owner known from `ProjectDetailsDto.owner.id`) and on the task page (owner looked up with `GET /api/projects/{projectId}`). In the all-tasks list a row shows a status control only when the task is assigned to the caller (Admin: every row). |
| 2 | AI button disabled, "Coming soon", Admin and PM only; wired last. Wired on 2026-11-12. | See non-goals. |
| 3 | **Only the dashboard is live.** | `dashboard-changed` re-fetches summary and upcoming only on the dashboard. Lists never refresh by themselves; new notifications still update the toast and badge everywhere. |
| 4 | Any status to any status (PRD 8); no final states. | Every status control offers all four statuses. |
| 5 | No "Viewing as" switcher, no demo accounts. | The role comes only from the signed-in user. |
| 6 | Dev server runs on **HTTPS** at `https://localhost:4200` with the ASP.NET Core dev certificate. | Section 11. |
| 7 | E2E with Playwright, local only, Admin credentials from environment variables, a fresh TM registered per run. Built 2026-11-11 (first deferred, then requested). | Section 13. |
| 8 | Unit tests stay in scope (services, guards, interceptors, SSE client, key components); E2E and CI were first deferred, then built on request (2026-11-11). | Section 13. |

## 3. Technology (verified 2026-11-11 with `npm view`)

| Item | Version / choice | Evidence |
| --- | --- | --- |
| Angular (`@angular/core`, `@angular/cli`) | 22.2.x (22.2.5 / 22.2.6), MIT | `npm view`; latest stable major, satisfies PRD 11 (18 or later) |
| Node.js | `^22.22.3 \|\| ^24.15.0 \|\| >=26.0.0` required; this machine has 22.23.1 | `npm view @angular/core engines` |
| TypeScript | **6.0.x** (Angular 22 requires `>=6.0 <6.1`; TypeScript 7 is not supported) | `@angular/compiler-cli@22.2.5` peerDependencies |
| Unit tests | **Vitest**, the `ng new` default (`testRunner` default `"vitest"`) | `@schematics/angular@22.2.6` `ng-new/schema.json` |
| Lint / format | angular-eslint 22.6.0 (needs eslint 9 or 10), eslint 10.14.0, prettier 3.9.10; all MIT | `npm view` |
| E2E | @playwright/test 1.65.0, Apache-2.0 | `npm view` |

Rules: re-check each exact version with `npm view <pkg> version` when installing
(versions move); pin exact versions; commit `package-lock.json`; `npm audit`
with no high or critical findings. Use what `ng new` generates (standalone,
strict) and do not add packages beyond this table without asking.

## 4. Structure

The app lives in `SmartTaskManagement-Frontend/` (ADR 0019); the Angular project
is generated at that folder's root [builder's call: one app, no workspace
libraries].

```text
src/app/
  core/            singletons, provided once in app.config.ts
    auth/          auth store (signals), auth API service, auth + role guards,
                   access-token and refresh interceptors
    http/          API base URL token, error interceptor, ProblemDetails parser,
                   errorCode -> message map
    realtime/      SSE client (fetch + ReadableStream), event parser, reconnect
    notifications/ notification store (unread count, list), toast service
    layout/        app shell: sidebar, top bar, bell, mobile drawer
    config/        environment access
  shared/          UI building blocks with no feature knowledge: badges
                   (status, priority), pager, confirm dialog, combobox,
                   empty/error/loading states, date and enum-label pipes
  features/
    auth/          login and register pages
    dashboard/     dashboard page and its cards
    projects/      list, detail, project dialogs; data-access service; models
    tasks/         all-tasks list, task detail, task dialogs; data-access; models
    users/         Admin user list and role change; data-access; models
    notifications/ panel (desktop) and full-screen page (mobile)
```

Each feature has `pages/`, `components/`, `data-access/` (one service per API
resource, returning typed observables) and `models/` (TypeScript types named
like the `openapi.json` schemas, e.g. `TaskListItemDto`). Models are written by
hand from `openapi.json` [builder's call]; a generator would add a dependency for
about 30 types. Enum values are the API strings (`ToDo`, `InProgress`,
`ProjectManager`, ...; the API serializes enums as strings) and are mapped to
display labels in one place.

Feature boundaries [decided 2026-11-11, after the final review]: a feature uses
another feature only through that feature's public API, its `index.ts` (today:
`features/tasks/index.ts` exports `ProjectTasksComponent` for the project page);
`no-restricted-imports` in `eslint.config.js` rejects imports of another
feature's internal `components/` or `pages/`. Pattern from Sheriff
(https://github.com/sheriff-arch/sheriff, "public APIs through `index.ts`
files"); Nx enforces module boundaries with a lint rule too.

Conventions: standalone components only, `ChangeDetectionStrategy.OnPush`
everywhere, signals for local and store state, `inject()` for dependencies,
typed reactive forms, no `any`. Components never call `HttpClient` directly.

## 5. Routes and guards

All feature routes are lazy (`loadComponent` / `loadChildren`).

| Path | Screen (prototype) | Access |
| --- | --- | --- |
| `/login` | D01 | Signed out only (a signed-in user is sent to `/dashboard`) |
| `/register` | M01 phone 1 | Signed out only |
| `/dashboard` | D02 | Any signed-in role |
| `/projects` | D04 | Any signed-in role |
| `/projects/:id` | D05 | Any signed-in role (the API returns 404 if not visible) |
| `/tasks` | D06 | Any signed-in role |
| `/tasks/:id` | D07 | Any signed-in role (404 if not visible) |
| `/users` | D09 | **Admin** |
| `/notifications` | M01 phone 6 (mobile full screen) | Any signed-in role |
| `**` | D10 404 page | Anyone |

- `authGuard`: if there is no access token, try one silent refresh (section 6);
  on failure go to `/login?returnUrl=...` (only same-app paths are accepted as
  `returnUrl`, never absolute URLs).
- `roleGuard(roles)`: reads the role from the auth store; a wrong role goes to
  the 404 page (consistent with the API, which hides what you may not see).
- Guards are UX only; the API enforces every rule.

## 6. Authentication (PRD 5, ADR 0014-0016, `auth-design.md`)

- **Access token in memory only**, in the auth store signal (with `expiresAt`
  and `user`). Never written to any storage.
- **Refresh token only in the HttpOnly cookie** `__Secure-refresh_token`
  (`Path=/api/auth`, `SameSite=Strict`, `Secure`). The app never reads it. The
  four auth calls, `POST /api/auth/register`, `login`, `refresh` and `logout`,
  use `withCredentials: true`: register and login **set** the cookie, and a
  cross-origin response's `Set-Cookie` is ignored without credentials
  (`auth-design.md`: "Angular sends the cookie with `withCredentials: true` on
  the auth calls only"). No other call uses credentials.
- **App start / reload:** call refresh once; success restores the session
  (token + user), 401 means signed out. The first route waits for this result.
- **Access-token interceptor:** adds `Authorization: Bearer` only to requests for
  the configured API base URL (never to other hosts).
- **Refresh interceptor:** on a 401 from a normal API call, start **one**
  refresh shared by all concurrent requests (one in-flight observable), then
  replay each failed request once with the new token. If the refresh fails, clear
  the session and go to `/login` with the "session ended" message (D10). Never
  try to refresh the refresh call itself or login/register.
- **Grace period (ADR 0016):** a refresh answered with 401
  `auth.refresh_superseded` (another tab rotated the token less than 30 s ago) is
  retried **once**, after a short delay, with the cookie the browser now has; any
  other refresh 401 (`auth.invalid_refresh_token`, missing cookie) ends the
  session.
- **Proactive refresh** [builder's call]: refresh about one minute before
  `expiresAt` while the app is open, so the SSE stream and calls rarely hit 401.
- **Logout:** call `POST /api/auth/logout` (204, idempotent), then always clear
  the in-memory session, close the SSE stream, clear notification state and go to
  `/login`, even if the call fails.
- **Role change:** the API revokes the user's tokens; the next call gets 401
  (`auth.token_revoked`), the refresh fails, and the user signs in again and gets
  the new role. The UI never caches the role beyond the in-memory session.
- **Origin check:** refresh and logout return 403 `auth.origin_not_allowed` when
  the page origin is not in `Cors:AllowedOrigins` (section 11); show it as a
  configuration error, not as "wrong password".
- Login shows the same message for unknown email and wrong password
  (`auth.invalid_credentials`).

## 7. HTTP errors (ProblemDetails with `errorCode` and `traceId`)

One error interceptor parses ProblemDetails; pages receive a typed error, never
raw `HttpErrorResponse`. `errorCode` and `traceId` are ProblemDetails extension
members that the API sends (`ErrorResults.cs`) but `openapi.json` does not
declare; the frontend types them as **optional** and falls back to the HTTP
status when they are missing.

| HTTP / errorCode | UI behaviour |
| --- | --- |
| 400 `validation.failed` | Each message from `errors` under its form field; unknown fields as a form-level alert |
| 401 (any call except auth) | Refresh flow (section 6) |
| 403 | "You don't have permission to do that." (should be rare: buttons follow the role) |
| 403 `auth.origin_not_allowed` | "This app's address is not allowed by the server (CORS settings)." |
| 404 / `*.not_found` | Detail pages show the 404 page; actions show "This item no longer exists or you no longer have access." |
| 409 `concurrency.conflict` | "Someone else changed this. Reload to see the latest version." + Reload |
| 409 `users.owns_projects` | Users page: message + link to that user's projects (`/projects?ownerId=`) |
| 409 `users.last_admin` | "There must be at least one Admin." |
| 409 `users.email_taken` | Register: under the email field |
| 409 `persistence.duplicate_key` | Generic "This already exists." |
| `projects.invalid_owner`, `projects.owner_not_allowed` | "The owner must be a Project Manager." |
| `tasks.invalid_assignee`, `tasks.assignee_not_allowed` | "This person can't be assigned to this task." |
| 429 | "Too many requests. Wait a minute and try again." |
| 503 (incl. `auth.unavailable`) | "The service is temporarily unavailable." + Try again |
| 0 / network | "Can't reach the server. Check your connection." |
| 500 / other | Generic message; `traceId` shown small for support; never server details |

Error texts live in one map keyed by `errorCode` (with an HTTP-status fallback),
so the mapping is unit-tested in one place.

## 8. Real time (SSE, PRD 13, `notifications-design.md` section 6)

- `GET /api/notifications/stream` needs the bearer header, so the client uses
  **`fetch` + `ReadableStream`** with `Authorization`, not `EventSource`. The
  token is **never** put in the URL.
- A small parser reads the `text/event-stream` format (`event:`, `data:`, blank
  line ends an event) [builder's call: hand-written, about 50 lines, unit-tested;
  no package].
- Events: `notification` (data = `NotificationDto`) -> toast, prepend to list,
  badge + 1; `notifications-read` -> re-fetch `unread-count`;
  `dashboard-changed` -> if the dashboard is open, re-fetch summary and
  upcoming; `heartbeat` -> ignore.
- The stream opens after sign-in and closes on logout (`AbortController`).
- **Reconnect:** the server ends the stream at token expiry or on a role change.
  When the stream ends or fails, reconnect with a valid token (refresh first if
  needed) using exponential backoff (1 s, 2 s, 4 s ... capped at 30 s, with
  jitter) [builder's call: numbers]; reset after a successful open. Stop when
  signed out. A 401 on connect goes through the refresh flow once.
- The dashboard shows "Live" / "Reconnecting..." (D02, D10). After a reconnect
  the dashboard re-fetches once, because events may have been missed.
- Toasts: `role="status"`, `aria-live="polite"`, dismiss button, auto-hide after
  about 6 s [builder's call], "Open task" link when the notification has a
  `taskId`. Only `notification` events produce toasts.

## 9. Screens and permissions

### 9.1 Role rules in the UI (PRD 2-3)

| UI element | Admin | PM | TM |
| --- | --- | --- | --- |
| Users menu and page | Yes | No | No |
| New project | Yes (chooses PM owner) | Yes (is the owner) | No |
| Edit / delete project | All | Own (`owner.id == me`) | No |
| Change owner | Yes | No | No |
| Add task (project page) | All projects | Own projects | No |
| Edit / assign / delete task | All | Tasks in own projects (owner checked, decision 1) | No |
| Status control | All tasks | Own projects (project and task pages) + tasks assigned to me | Tasks assigned to me |
| Assignee filter "choose a person" | Yes | Yes | No (it needs `/api/users/assignable`, which a TM may not call) |
| Owner filter on projects | Yes | No | No |
| Improve with AI (task dialogs) | Yes | Yes | Hidden |

New tasks are created from a project page, where the project is known
[decided in the screens]. The dashboard subtitle and the "Tasks" info line
explain the scope per role (D02, M01).

### 9.2 Assignment coverage (evidence)

| Brief requirement | Screen |
| --- | --- |
| Login / Register / Logout | D01 / M01 phone 1 / sidebar and drawer |
| Role-based authorization | Section 9.1; Admin D04, D05, D09; PM D02, D06, D07; TM M01 |
| Create / update / delete project | D08 New project / D11 Edit project / D08 Delete |
| View, list, search, sort, paginate projects | D05 / D04 |
| Create / update / delete task | D08 New task / D11 Edit task / D11 Delete task |
| Assign task, update status, priority, due date | D11 Assign, D08 (on create) / D05, D06, D07, M01 / D08, D11 |
| Search, filter, sort, paginate tasks | D05, D06, M01 phone 4 |
| Dashboard (6 metrics) | D02, M01 phone 2 (+ overdue) |
| Responsive UI | M01 (360 px); desktop D01-D11 |

### 9.3 API usage (evidence: every non-sample endpoint)

| API | Screen | Action |
| --- | --- | --- |
| `POST /api/auth/register` | M01-1 | Create account |
| `POST /api/auth/login` | D01 | Sign in |
| `POST /api/auth/refresh` | none (background) | App start, 401 recovery, proactive refresh |
| `POST /api/auth/logout` | Sidebar, drawer | Log out |
| `GET /api/auth/me` | not used | `AuthResponse.user` is enough |
| `GET /api/users` | D09; D04 owner filter; D08 owner pickers | User list; `Role=ProjectManager` search |
| `GET /api/users/assignable` | D08 New task, D11 Assign, assignee filters | Assignee search (Admin, PM) |
| `PUT /api/users/{id}/role` | D09 | Change role (after confirmation) |
| `POST /api/projects` | D08 | Create project |
| `GET /api/projects` | D04; D06 project filter | List, search, sort, page, owner filter |
| `GET /api/projects/{id}` | D05; D07 | Detail; owner check (decision 1) |
| `PUT /api/projects/{id}` | D11 | Edit project |
| `DELETE /api/projects/{id}` | D08 | Delete project |
| `PUT /api/projects/{id}/owner` | D08 | Change owner (Admin) |
| `POST /api/projects/{projectId}/tasks` | D08 | Create task (optional assignee) |
| `GET /api/projects/{projectId}/tasks` | D05 | Project task list |
| `GET /api/tasks` | D06, M01-4 | All visible tasks |
| `GET /api/tasks/{id}` | D07, M01-5 | Task detail (notification target) |
| `PUT /api/tasks/{id}` | D11 | Edit task |
| `DELETE /api/tasks/{id}` | D11 | Delete task |
| `PUT /api/tasks/{id}/status` | D05, D06, D07, M01 | Change status |
| `PUT /api/tasks/{id}/assignee` | D11, D07 | Assign / change / unassign (`null`) |
| `GET /api/dashboard/summary` | D02, M01-2 | Cards and charts |
| `GET /api/dashboard/upcoming` | D02, M01-2 | Upcoming list |
| `GET /api/notifications` | D03, M01-6 | Notification list |
| `GET /api/notifications/unread-count` | Bell on every screen | Badge |
| `POST /api/notifications/{id}/read` | D03 | Mark read; also on opening an item |
| `POST /api/notifications/read-all` | D03, M01-6 | Mark all read |
| `GET /api/notifications/stream` | Shell (D02 Live, D10) | SSE |

### 9.4 Screen notes beyond the prototype

- **Lists** (projects, project tasks, all tasks, users): server-side search,
  filter, sort and paging with the API's query names (`Search`, `SortBy`,
  `SortDirection` = `Ascending`/`Descending`, `Page`, `PageSize`, plus each
  list's filters). Default page size 20, options 10/20/50/100 (API maximum 100).
  Total pages = ceil(`totalCount` / `pageSize`). Search is debounced (about 300
  ms) [builder's call]. Filters, sort and page live in the URL query string so a
  reload or the back button keeps them [builder's call].
- Sort fields: projects `name`, `createdAt`, `updatedAt`; tasks `title`,
  `priority`, `dueDate`, `status`, `createdAt`, `updatedAt`; users `fullName`,
  `email`, `role`, `createdAt`.
- **Project list** shows name, owner, task progress (`completedTaskCount` /
  `taskCount`), created and updated; no description (not in the list DTO).
- **Dashboard:** five cards (total projects, total tasks, completed, pending,
  overdue), status and priority bars, completed-vs-pending donut, upcoming list
  (pending tasks due today to today + 7 days; the API decides the window).
  Overdue card links to the tasks list [builder's call: with `dueTo` = yesterday
  and status filter if expressible; otherwise to the plain list].
- **Due dates** are date-only strings (`YYYY-MM-DD`). "Today" / "Overdue" labels
  use the **UTC** date, like the API (PRD 9) [builder's call]; dates are shown in
  the user's locale format without time-zone shifts.
- **Notification panel:** last 7 days from `GET /api/notifications`; unread
  items marked by a dot plus visually hidden text "Unread" and bold text; click
  opens `/tasks/{taskId}` (or `/projects/{projectId}`) and marks it read;
  removed-type items (`TaskUnassigned`, `ProjectOwnerRemoved`) have no link.
  Desktop: popover anchored to the bell; mobile: `/notifications` page.
- **Users:** a role change asks for confirmation and says the person will be
  signed out everywhere. The Admin's own row is not special-cased; the API
  answers `users.last_admin` when needed.
- **Mobile (below about 768 px):** top bar with menu button, title and bell;
  the sidebar becomes a drawer (focus moves into it, Esc closes it); task lists
  render as cards; dialogs become full-screen sheets; filters sit behind a
  "Filters" button.

## 10. Forms and validation (UX hints that match the API)

| Form | Field | Rule shown in the UI |
| --- | --- | --- |
| Register | Full name | required, max 100 |
| Register | Email | required, email format, max 256 |
| Register | Password | 8-128, at least one letter and one digit (live checklist) |
| Register | Confirm password | must match (client-only; not sent) |
| Login | Email, password | required (no strength rules on login) |
| Project | Name | required, max 200 |
| Project | Description | optional, max 2000 |
| Project (Admin create) | Owner | required, a Project Manager |
| Task | Title | required, max 200 |
| Task | Description | optional, max 4000 |
| Task | Priority | Low/Medium/High/Critical, default Medium |
| Task | Due date | optional, date only, past dates allowed |
| Task (create) | Assignee | optional (assignable search) |

Counters show the remaining length. Submit buttons are disabled while sending
(no double submit). Server validation messages (400) always win over the hints.

## 11. Configuration and local development

- `environment.ts` / `environment.development.ts` hold only the **API base URL**
  (no secrets). Default development value: `https://localhost:7254` (the API's
  `https` launch profile, `SmartTaskManagement-Backend/README.md`).
- **Dev server on HTTPS** [decided]: `ng serve` with `ssl: true` and
  `sslCert`/`sslKey` pointing to the ASP.NET Core development certificate,
  exported to PEM with `dotnet dev-certs https --export-path <file> --format Pem
  --no-password`. The exported files are git-ignored and never committed.
  Reason: with HTTP on 4200 and HTTPS on 7254 the two are cross-site under
  schemeful same-site (web.dev "Schemeful Same-Site"; MDN "Site": the port does
  not matter, the scheme does), so the `SameSite=Strict` refresh cookie would be
  blocked. UNVERIFIED until run: that the exported certificate works with
  `ng serve` and the browser trusts it; prove it in milestone 1.
- **CORS (backend setting, the developer sets it; the frontend does not change
  backend config):** `Cors:AllowedOrigins` = `["https://localhost:4200"]`, set as
  a user-secret on the API project (`dotnet user-secrets set
  "Cors:AllowedOrigins:0" "https://localhost:4200"` in
  `SmartTaskManagement-Backend/src/Api`), so no tracked file changes.
- **Email links:** `Email:AppBaseUrl` in the backend's committed
  `appsettings.Development.json` is `http://localhost:4200`; with the HTTPS dev
  server, links in notification emails must use `https://localhost:4200`. See
  section 15.
- **Production hosting is out of scope** for this version [builder's call,
  because of the deadline; the developer may override]: the production build must compile and holds a placeholder API URL;
  no frontend Dockerfile, compose or web server yet. Because the refresh cookie is
  `SameSite=Strict`, a later deployment must serve the app and the API on the
  same site.
- The README explains: start Docker Desktop, `docker compose up -d` in the
  backend folder, run the API, export the certificate, `npm ci`, `npm start`.

## 12. Accessibility and visual design

- Visual language from the prototype: Inter (body) and Space Grotesk
  (headings), teal primary `#0F766E`, slate sidebar `#0F172A`, status and priority
  badge colours as drawn. Colours are CSS custom properties in one global file.
- Fonts [builder's call]: load from Google Fonts like the prototype, or fall back
  to system fonts; no font package.
- WCAG 2.1 AA: text contrast at least 4.5:1 (the prototype colours were chosen
  for this); visible focus ring on every control; real `<button>`, `<a>`,
  `<label>`; icon buttons with `aria-label`; dialogs trap focus, close on Esc and
  return focus; `aria-current` in navigation; live regions for toasts and
  validation summaries; touch targets at least 44 px on mobile.
- Works from 360 px wide to desktop with no horizontal page scroll (wide tables
  scroll inside their box).

## 13. Quality gates

- **ESLint** (angular-eslint) and **Prettier**: zero errors; Prettier check in
  verify.
- **Type-check:** strict TypeScript plus `strictTemplates`, no `any`.
- **Unit tests (Vitest), in scope now:**
  - auth store and auth API service;
  - access-token interceptor (only the API host), refresh interceptor (one
    shared refresh for concurrent 401s, replay once, `refresh_superseded` retry,
    failure ends the session);
  - error interceptor and the `errorCode` map;
  - `authGuard`, `roleGuard`, `returnUrl` safety;
  - SSE parser and client (events, reconnect with backoff, abort on logout);
  - notification store (badge, read, read-all, live event);
  - key components: login form, task status control (role rules), pager,
    notification panel.
- **Verify scripts:** `scripts/verify.sh` (and `scripts/verify.ps1` if
  practical) run lint, Prettier check, type-check, unit tests, production build
  and `npm audit --audit-level=high`, and fail on the first error. Each
  milestone ends with this script green (and the backend verify when files
  outside the frontend folder change).
- **E2E** (Playwright 1.65.0, local only, built 2026-11-11; drives the installed
  Edge, `channel: msedge`): login; create a project;
  create and assign a task; the assignee receives the notification (toast +
  badge). Admin credentials from `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD`
  (never committed, never printed); a fresh Team Member is registered with a
  unique email per run; test data stays (soft delete only).
- **CI** `.github/workflows/frontend.yml` (built 2026-11-11 at the developer's request): triggers on
  `paths: ['*-Frontend/**', '.github/workflows/frontend.yml']`; finds the
  frontend folder by the `*-Frontend` pattern like `backend.yml` (no project
  name); Node version from the frontend `.nvmrc`; `npm ci`; runs
  `scripts/verify.sh`. No E2E in CI.
- Docs to add with the code: `SmartTaskManagement-Frontend/CLAUDE.md` (frontend
  rules), `SmartTaskManagement-Frontend/README.md`, and pointer lines in the
  root `README.md` and `CLAUDE.md`.

## 14. Milestones (outline for the plan)

1. Scaffold, tooling and dev HTTPS: Angular app, lint, Prettier, Vitest, verify
   script, environment config, HTTPS with the dev certificate proven, CLAUDE.md,
   README.
2. Core: auth store and interceptors, error handling, guards, app shell
   (sidebar, top bar, drawer), login and register.
3. Projects: list, detail, create, edit, delete, change owner.
4. Tasks: project task list, all-tasks list, task detail, create, edit, assign,
   status, delete.
5. Dashboard and Users.
6. Notifications and SSE: bell, panel, toasts, live dashboard, reconnect.
7. Polish: responsive pass, accessibility pass, states (D10), docs, final review.
8. Deferred: E2E (Playwright), CI workflow.
9. Last: AI button wiring, after the AI endpoint exists (done 2026-11-12).

## 15. Open items and UNVERIFIED

- UNVERIFIED: `ng serve` HTTPS with the exported ASP.NET Core dev certificate
  (milestone 1 proves it).
- UNVERIFIED: browser handling of the `__Secure-` refresh cookie across
  `https://localhost:4200` and `https://localhost:7254` (same site, different
  origin) with `withCredentials`; proven when login and reload work in
  milestone 2. If it fails, stop and report; do not weaken the cookie.
- ASSUMPTION: the API runs with the `https` launch profile on port 7254 during
  frontend development.
- Open (developer): `Email:AppBaseUrl` for HTTPS dev (section 11).
- Contract gaps noticed, no frontend impact: `openapi.json` does not declare
  `errorCode`/`traceId` (section 7); PRD 5 says the cookie path covers only
  refresh and logout, while `auth-design.md` uses `Path=/api/auth` (it also covers
  login, register and `/me`).
