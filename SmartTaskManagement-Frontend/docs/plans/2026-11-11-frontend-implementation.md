# Frontend (Angular) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Angular frontend in `SmartTaskManagement-Frontend/` for the
brief's frontend list (P1) and, if time allows, notifications/SSE and the Users
page (P2), in a 4-hour window.

**Architecture:** One standalone Angular 22 app at the frontend folder root.
`core/` holds auth (in-memory token, single-flight refresh), HTTP error mapping,
guards, shell and the SSE client; `shared/` holds UI building blocks; `features/`
holds lazy-loaded pages with one data-access service per API resource. The API
(`../../SmartTaskManagement-Backend/docs/openapi.json`) is the authority.

**Tech Stack:** Angular 22.2.x (CLI 22.2.6), TypeScript 6.0.x, Vitest (CLI
default), angular-eslint 22.6.x, ESLint 10.x, Prettier 3.9.x, plain CSS.

**Spec:** `SmartTaskManagement-Frontend/docs/frontend-design.md` (read it with
this plan; screens in `SmartTaskManagement-Frontend/docs/design/prototype/`).

## Scope (agreed 2026-11-11, 4 hours)

- **P1 (must):** Tasks 1-8, checkpoint after Task 4 and after Task 8.
- **P2 (if time, in order):** Task 9 (notifications + SSE), Task 10 (Users).
- **Deferred (not built now):** E2E (Playwright), CI `frontend.yml`, AI button
  wiring (last, after the AI endpoint exists), Change owner dialog, component
  tests beyond those listed, `Email:AppBaseUrl` backend fix (separate backend
  task, needs the developer's OK).

## Global Constraints

- No backend code or config change. Do not touch `SmartTaskManagement-Backend/`.
- Access token in memory only; never `localStorage`/`sessionStorage`/URL.
- `withCredentials: true` on exactly `POST /api/auth/register|login|refresh|logout`; on no other call.
- Bearer header only on requests whose URL starts with the API base URL.
- Only endpoints and fields from `openapi.json`; enums are the API strings (`ToDo`, `InProgress`, `Completed`, `Cancelled`; `Low`..`Critical`; `Admin`, `ProjectManager`, `TeamMember`); `SortDirection` = `Ascending`/`Descending`.
- Field limits: full name 100, email 256, password 8-128 with a letter and a digit, project name 200, project description 2000, task title 200, task description 4000; page size default 20, max 100.
- Any task status may change to any status.
- Standalone components, `ChangeDetectionStrategy.OnPush`, signals, `inject()`, typed reactive forms, strict TS + `strictTemplates`, no `any`.
- Pin exact npm versions; commit `package-lock.json`; `npm audit --audit-level=high` clean. Verify every version with `npm view <pkg> version` before installing.
- Dev server `https://localhost:4200`; API `https://localhost:7254`. Never commit the exported certificate files.
- Commits: `feat:`/`fix:`/`docs:`, one logical change each, repository-local identity, no amend, no force-push; never add `.vscode/`.
- Never read `.env` files or print secret values.

## Review Focus

1. Several requests get 401 at the same moment -> exactly one `POST /api/auth/refresh`, then each request replays once (Task 3 test).
2. Refresh answers 401 `auth.refresh_superseded` -> one retry, then success or sign-out (Task 3 test).
3. `returnUrl` is an absolute or protocol-relative URL (`https://evil`, `//evil`) -> ignored, go to `/dashboard` (Task 4 test).
4. A request to a host other than the API -> no `Authorization` header (Task 3 test).
5. An SSE event split across two network chunks -> parsed as one event (Task 9 test).

---

### Task 1: Scaffold, tooling, HTTPS dev server, verify script

**Files:**
- Create (generated): `SmartTaskManagement-Frontend/{package.json,package-lock.json,angular.json,tsconfig*.json,src/**}`
- Create: `SmartTaskManagement-Frontend/{.nvmrc,.prettierrc.json,.prettierignore,eslint.config.js,.gitignore,CLAUDE.md}`, `scripts/verify.sh`
- Modify: `SmartTaskManagement-Frontend/README.md`

- [ ] **Step 1:** Generate into a temp folder (keeps the existing README and `docs/`): `npx -y @angular/cli@22.2.6 new smart-task --directory <scratch>/smart-task --skip-git --skip-install --style css --ssr false --routing --zoneless --interactive=false`; copy everything except `README.md` and `.git*` into `SmartTaskManagement-Frontend/`. Expected: `angular.json` with `"test"` builder using Vitest.
- [ ] **Step 2:** `npm install` (exact versions: run `npm config set save-exact true --location=project` first); `npx ng add angular-eslint@22.6.0 --skip-confirmation`; `npm i -D prettier@<verified>`. Add scripts: `lint`, `format:check` (`prettier --check .`), `test:ci` (`ng test --watch=false`), `build` (production).
- [ ] **Step 3:** `.nvmrc` = `22`; `.gitignore` adds `/certs/`. Add `serve` options in `angular.json`: `"ssl": true, "sslCert": "certs/localhost.pem", "sslKey": "certs/localhost.key"`. README gets the cert export command: `dotnet dev-certs https --export-path certs/localhost.pem --format Pem --no-password` (writes `localhost.pem` + `localhost.key`; UNVERIFIED file names: check what it writes and adjust).
- [ ] **Step 4:** `src/environments/environment.ts` (`apiBaseUrl: 'https://api.example.invalid'` placeholder) and `environment.development.ts` (`apiBaseUrl: 'https://localhost:7254'`), with `fileReplacements` for development.
- [ ] **Step 5:** `scripts/verify.sh` (bash, `set -euo pipefail`, runs from its own folder's parent): `npm run lint`, `npm run format:check`, `npx tsc -p tsconfig.app.json --noEmit`, `npm run test:ci`, `npm run build`, `npm audit --audit-level=high`. Run it. Expected: all steps pass, exit 0.
- [ ] **Step 6:** Prove HTTPS: `npm start`, open `https://localhost:4200` with no certificate warning. Record the result in README (evidence). If it fails: stop and report.
- [ ] **Step 7:** `CLAUDE.md` (frontend rules = Global Constraints + structure from spec section 4); README (run steps, CORS user-secret command from spec section 11).
- [ ] **Step 8:** Commit `feat: scaffold the Angular frontend with lint, tests and verify`.

### Task 2: API models, base URL and error mapping

**Files:**
- Create: `src/app/core/config/api-base-url.ts`, `src/app/core/models/api.models.ts`, `src/app/core/http/api-problem.ts`, `src/app/core/http/error-messages.ts`
- Test: `src/app/core/http/api-problem.spec.ts`

**Interfaces:**
- Produces: `API_BASE_URL: InjectionToken<string>` (provided from `environment.apiBaseUrl` in `app.config.ts`); types `Role`, `TaskItemStatus`, `TaskPriority`, `SortDirection`, `PagedResult<T> {items; page; pageSize; totalCount}`, `CurrentUserDto`, `AuthResponse`; `interface ApiProblem { status: number; errorCode?: string; title?: string; detail?: string; traceId?: string; fieldErrors: Record<string, string[]> }`; `toApiProblem(e: unknown): ApiProblem`; `messageFor(p: ApiProblem): string`.

- [ ] **Step 1: Failing tests:** `toApiProblem` reads `errorCode`, `traceId`, `errors` from a ProblemDetails body; status 0 -> `status: 0`; `messageFor` returns the spec section 7 texts for `auth.invalid_credentials`, `concurrency.conflict`, `users.owns_projects`, `users.last_admin`, `auth.origin_not_allowed`, 429, 503, 0, and the generic text for an unknown 500.
- [ ] **Step 2:** Run `npx ng test --watch=false` -> FAIL (missing module).
- [ ] **Step 3:** Implement; texts copied from spec section 7.
- [ ] **Step 4:** Run -> PASS. **Step 5:** Commit `feat: add API models and error mapping`.

### Task 3: Auth store, auth API, session and interceptors

**Files:**
- Create: `src/app/core/auth/{auth.store.ts,auth-api.service.ts,session.service.ts,auth-token.interceptor.ts,refresh.interceptor.ts,error.interceptor.ts}`
- Test: `src/app/core/auth/{session.service.spec.ts,interceptors.spec.ts}`

**Interfaces:**
- Produces: `AuthStore` (`user: Signal<CurrentUserDto|null>`, `accessToken: Signal<string|null>`, `isSignedIn: Signal<boolean>`, `role: Signal<Role|null>`, `setSession(r: AuthResponse): void`, `clear(): void`); `AuthApi` (`login(body): Observable<AuthResponse>`, `register(body)`, `refresh()`, `logout(): Observable<void>`); `SessionService` (`refresh(): Observable<boolean>` single-flight via `shareReplay`, `restore(): Promise<boolean>`, `logout(): Promise<void>`, `sessionEnded: Signal<boolean>`); `authTokenInterceptor`, `refreshInterceptor`, `errorInterceptor` (maps to `ApiProblem` via `toApiProblem`) as `HttpInterceptorFn`, registered in that order.

- [ ] **Step 1: Failing tests (HttpTestingController):**
  - `authTokenInterceptor_AddsBearer_OnlyForApiBaseUrl` (API URL has header; `https://other.example/x` has none).
  - `AuthApi_UsesWithCredentials_OnlyOnAuthCalls` (login/register/refresh/logout true; `GET /api/projects` false).
  - `refreshInterceptor_ConcurrentUnauthorized_RefreshesOnce` (3 requests 401 -> 1 refresh request -> 3 replays with the new token).
  - `refresh_Superseded_RetriesOnce` (first refresh 401 `auth.refresh_superseded`, second 200 -> signed in; two 401s -> signed out).
  - `refreshFailure_ClearsSession_SetsSessionEnded`.
  - `refreshInterceptor_DoesNotRefresh_ForAuthEndpoints` (401 on login is passed through).
  - `logout_ClearsSession_EvenWhenCallFails`.
- [ ] **Step 2:** Run -> FAIL. **Step 3:** Implement (proactive refresh timer 60 s before `expiresAt`). **Step 4:** Run -> PASS.
- [ ] **Step 5:** Commit `feat: add in-memory session with single-flight token refresh`.

### Task 4: Guards, routes, shell, login and register  (then CHECKPOINT 1)

**Files:**
- Create: `src/app/core/auth/guards.ts`, `src/app/core/layout/{shell.component.ts,shell.component.html,shell.component.css}`, `src/app/features/auth/{login.page.ts,register.page.ts}` (+ html/css), `src/app/features/not-found.page.ts`, `src/styles.css` (tokens from prototype)
- Modify: `src/app/app.routes.ts`, `src/app/app.config.ts` (`provideAppInitializer(() => inject(SessionService).restore())`)
- Test: `src/app/core/auth/guards.spec.ts`

**Interfaces:**
- Produces: `authGuard`, `guestGuard`: `CanActivateFn`; `roleGuard(...roles: Role[]): CanActivateFn`; `safeReturnUrl(url: string | null): string` (only paths starting with a single `/`, else `/dashboard`); routes per spec section 5 (lazy `loadComponent`), feature routes as children of `ShellComponent`.

- [ ] **Step 1: Failing tests:** `safeReturnUrl('https://evil')`, `('//evil')`, `(null)` -> `/dashboard`; `('/tasks/1')` -> `/tasks/1`; `authGuard` signed out -> `UrlTree` `/login?returnUrl=...`; `roleGuard('Admin')` as `TeamMember` -> 404 route.
- [ ] **Step 2:** Run -> FAIL. **Step 3:** Implement guards, routes, shell (sidebar with Dashboard/Projects/Tasks/Users(Admin), user block, Log out; top bar; drawer below 768 px with Esc and focus), login (D01) and register (M01-1; live password checklist; `users.email_taken` under email). **Step 4:** Run -> PASS.
- [ ] **Step 5:** Commit `feat: add guards, app shell, login and register`.
- [ ] **CHECKPOINT 1:** run `bash scripts/verify.sh` and show the output; the developer sets the CORS user-secret, runs the API and checks: register, login, reload stays signed in, logout. Wait for the go-ahead.

### Task 5: Shared UI

**Files:** Create `src/app/shared/{labels.ts,pager.component.ts,status-badge.component.ts,priority-badge.component.ts,confirm-dialog.component.ts,user-picker.component.ts,list-query.ts}`; Test `src/app/shared/{labels.spec.ts,list-query.spec.ts}`

**Interfaces:**
- Produces: `statusLabel(s: TaskItemStatus): string`, `priorityLabel`, `roleLabel`; `PagerComponent` (inputs `page`, `pageSize`, `totalCount`; output `pageChange: number`, `pageSizeChange: number`; sizes 10/20/50/100); `ConfirmDialogComponent` (native `<dialog>`, inputs `title`, `message`, `confirmText`; output `confirmed`); `UserPickerComponent` (input `search: (term: string) => Observable<PagedResult<{id: string; fullName: string; email: string}>>`; output `picked: {id; fullName; email}`; combobox ARIA, 300 ms debounce); `readListQuery(params: ParamMap, defaults)` / `toQueryParams(q)` for URL-held list state.
- [ ] Tests: labels map all enum values (`ToDo` -> `To Do`, `ProjectManager` -> `Project Manager`); `toQueryParams` drops empty values and round-trips with `readListQuery`. Run FAIL -> implement -> PASS. Commit `feat: add shared list and dialog components`.

### Task 6: Projects

**Files:** Create `src/app/features/projects/{data-access/projects-api.service.ts,models/project.models.ts,pages/projects-list.page.ts,pages/project-detail.page.ts,components/project-form.dialog.ts}`, `src/app/features/users/users-api.service.ts`; Test `data-access/projects-api.service.spec.ts`

**Interfaces:**
- Produces (users, used by Tasks 6, 7, 10): `UsersApi` (`list(q: { search?; role?: Role; sortBy?: 'fullName'|'email'|'role'|'createdAt'; sortDirection?; page; pageSize }): Observable<PagedResult<UserDto>>`, `assignable(search: string, page = 1): Observable<PagedResult<AssignableUserDto>>`).
- Produces: `ProjectsApi` (`list(q: ProjectListQuery): Observable<PagedResult<ProjectListItemDto>>`, `get(id): Observable<ProjectDetailsDto>`, `create(b: CreateProjectRequest): Observable<{id: string}>`, `update(id, b: UpdateProjectRequest): Observable<void>`, `remove(id): Observable<void>`); `ProjectListQuery { search?; ownerId?; sortBy?: 'name'|'createdAt'|'updatedAt'; sortDirection?; page; pageSize }` sent as `Search`, `OwnerId`, `SortBy`, `SortDirection`, `Page`, `PageSize`.
- [ ] Test: `list` sends the PascalCase query names and omits empty ones. FAIL -> implement -> PASS.
- [ ] Pages per D04/D05/D08/D11: list (search, Admin owner filter via `GET /api/users?Role=ProjectManager`, sort, pager, progress `completedTaskCount/taskCount`); detail (description, owner, counts, Edit/Delete for Admin or `owner.id == me`); form dialog (create: Admin must pick owner; edit: name + description); delete confirm -> back to list.
- [ ] Commit `feat: add project list, detail and editing`.

### Task 7: Tasks

**Files:** Create `src/app/features/tasks/{data-access/tasks-api.service.ts,models/task.models.ts,pages/tasks-list.page.ts,pages/task-detail.page.ts,components/task-table.component.ts,components/task-form.dialog.ts,components/assign.dialog.ts,components/status-select.component.ts,task-permissions.ts}`; Test `task-permissions.spec.ts`, `data-access/tasks-api.service.spec.ts`

**Interfaces:**
- Produces: `TasksApi` (`listForProject(projectId, q)`, `list(q)`, `get(id)`, `create(projectId, b: CreateTaskRequest)`, `update(id, b: UpdateTaskRequest)`, `remove(id)`, `changeStatus(id, status)`, `assign(id, assigneeId: string | null)`); `TaskListQuery { search?; projectId?; status?; priority?; assigneeId?; unassigned?; dueFrom?; dueTo?; sortBy?: 'title'|'priority'|'dueDate'|'status'|'createdAt'|'updatedAt'; sortDirection?; page; pageSize }`; `canManageTask(role: Role, me: string, projectOwnerId: string | null): boolean`; `canChangeStatus(role, me, assigneeId, projectOwnerId: string | null): boolean`.
- [ ] Tests: `canManageTask` Admin true; PM true only when `projectOwnerId == me`; PM with `null` owner false; TM false. `canChangeStatus` assignee true for any role; PM owner true; TM not assignee false. FAIL -> implement -> PASS.
- [ ] `TaskTableComponent` (inputs `tasks`, `projectOwnerId: string | null`; status select when `canChangeStatus`, else badge; title links to `/tasks/:id`) used by project detail (owner known) and `/tasks` (owner `null`, decision 1). Task detail loads the task, then `ProjectsApi.get(task.project.id)` for the owner; shows Edit/Assign/Unassign/Delete only when `canManageTask`. Task form: title, priority (default Medium), due date (clearable), description with counter and **disabled "Improve with AI"** (tooltip "Coming soon", Admin/PM only), assignee picker on create. New task only from project detail.
- [ ] Commit `feat: add task lists, detail, editing and assignment`.

### Task 8: Dashboard  (then CHECKPOINT 2)

**Files:** Create `src/app/features/dashboard/{dashboard-api.service.ts,dashboard.page.ts}` (+ html/css), `src/app/shared/date-utils.ts`; Test `src/app/shared/date-utils.spec.ts`

**Interfaces:**
- Produces: `DashboardApi` (`summary(): Observable<DashboardSummaryDto>`, `upcoming(): Observable<UpcomingTaskDto[]>`); `DashboardPage.reload(): void` (Task 9 calls it); `utcToday(now: Date): string` (`YYYY-MM-DD`), `dueLabel(due: string, today: string): 'Overdue'|'Today'|'Tomorrow'|null`.
- [ ] Tests: `utcToday(new Date('2026-11-11T23:30:00-05:00'))` = `'2026-11-12'`; `dueLabel` cases. FAIL -> implement -> PASS.
- [ ] Page per D02: five cards (total projects, total tasks, completed, pending, overdue), status and priority bars, donut with text alternative, upcoming table (cards below 768 px). Summary and upcoming load in parallel.
- [ ] Commit `feat: add the dashboard`.
- [ ] **CHECKPOINT 2:** `bash scripts/verify.sh` output; root `README.md` and `CLAUDE.md` pointer lines for the frontend (commit `docs:`); manual check by the developer (projects, tasks, assign, status, dashboard as Admin, PM and TM); scan the diff for secrets; with the go-ahead push `feat/frontend` and open the PR with `.github/pull_request_template.md`.

### Task 9 (P2): Notifications and SSE

**Files:** Create `src/app/core/realtime/{sse-parser.ts,live-events.service.ts}`, `src/app/core/notifications/{notifications-api.service.ts,notification.store.ts,toast.service.ts,toast-host.component.ts}`, `src/app/features/notifications/{notification-panel.component.ts,notifications.page.ts}`; Test `sse-parser.spec.ts`, `live-events.service.spec.ts`, `notification.store.spec.ts`

**Interfaces:**
- Produces: `class SseParser { push(chunk: string): SseEvent[] }`, `interface SseEvent { event: string; data: string }`; `LiveEvents` (`start(): void`, `stop(): void`, `events: Observable<SseEvent>`, `status: Signal<'live'|'reconnecting'|'off'>`), fetch + `ReadableStream` + `AbortController`, bearer from `AuthStore`, backoff 1-2-4-8-16-30 s with jitter, refresh via `SessionService.refresh()` before reconnect; `NotificationStore` (`unread: Signal<number>`, `items: Signal<NotificationDto[]>`, `load()`, `markRead(id)`, `markAllRead()`, `onLive(dto)`).
- [ ] Tests: parser handles one event split over two chunks (Review Focus 5), CRLF lines, `heartbeat` with empty data; `LiveEvents` reconnects after the stream ends and stops on `stop()` (fake timers, mocked `fetch`); store increments `unread` on `notification` and refetches the count on `notifications-read`. FAIL -> implement -> PASS.
- [ ] Wire: start on sign-in, stop on logout; bell badge + panel (D03) / `/notifications` (M01-6); toast (D02, `role="status"`, 6 s); dashboard listens to `dashboard-changed` and reloads after reconnect; "Live"/"Reconnecting..." pill.
- [ ] Commit `feat: add notifications and live updates`. Run verify; push to the open PR.

### Task 10 (P2): Users (Admin)

**Files:** Create `src/app/features/users/users.page.ts`; Modify `src/app/features/users/users-api.service.ts`
- **Interfaces:** Consumes `UsersApi` (Task 6); adds `changeRole(id: string, role: Role): Observable<void>`.
- [ ] Page per D09: search, role filter, sort, pager, role select with confirm ("signed out everywhere"), `users.owns_projects` message with link `/projects?ownerId=<id>`, `users.last_admin` message.
- [ ] Commit `feat: add user role management`. Run verify; push.
