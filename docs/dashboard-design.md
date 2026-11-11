# Design: Dashboard

- Status: Implemented (2026-11-11). Approved by the developer (2026-11-11). Written from defaults at the
  developer's request; every decision not stated in the PRD is marked
  **[builder's call]**.
- Date: 2026-11-11
- Requirements: `docs/PRD.md` section 9 (assignment list, role scope) and
  sections 2 and 3 (visibility). Builds on the Project module
  (`docs/project-design.md`) and the Task module (`docs/task-design.md`).

## 1. Intent

Two read-only calls (section 5) that tell a signed-in user the state of the
work they can see: how many projects and tasks, how the tasks split by status
and priority, how much is done versus still open, and what is due soon. The
Angular dashboard (later) renders the summary cards and the upcoming list from
them.

Goals:

- The six numbers the assignment lists (PRD 9), nothing hidden by role mistakes.
- Exactly the same visibility as the task and project lists (PRD 3: "Dashboard
  numbers are scoped exactly like the lists above").
- Computed in the database (grouping, counts), never by loading tasks into memory.

Non-goals: live updates and notifications (the developer wants live push, a
stored notification list and email on assignment; they come in a separate
Notifications module after this one, likely with Server-Sent Events; the
dashboard API does not change for it: the client re-fetches on a "tasks
changed" signal), removing the Sample slice (the developer decided on 2026-11-11 to
keep it for now; `docs/CONTEXT.md` build order is corrected with this work),
charts or time series (trends over weeks), per-user workload, caching
(not allowed, `packages.md`), filters on the dashboard (the task list already
filters), overdue lists beyond one count (see section 3).

Anti-goals: a number that includes a task the caller cannot open; a number that
counts deleted tasks or tasks of deleted projects; a second visibility rule that
can drift from the lists.

## 2. Scope: whose numbers

The dashboard counts exactly what the caller sees in the lists:

- **Tasks:** the visible task set of `TaskItemReadRepository` (Admin all; owning
  Project Manager: every task of own projects; any user: tasks assigned to them;
  never deleted tasks or tasks of deleted projects).
- **Projects:** the visible project set of `ProjectReadRepository` (Admin all;
  owner; assignee of a non-deleted task in it).

So an Admin sees the whole system, a Project Manager sees own projects plus
tasks assigned to them elsewhere, a Team Member sees their assigned tasks and
those tasks' projects (PRD 2 dashboard row, PRD 3).

Developer decision (2026-11-11): the Project Manager's dashboard follows the
list rule (PRD 3), not "own projects only". PRD 2's dashboard row ("Own
projects") is corrected to match when this design is approved.

**One visibility definition [builder's call]:** the rule is written four times
today (`ProjectReadRepository.Visible` and `CountedTasks`,
`TaskItemReadRepository.VisibleTasks` and `IsProjectVisibleAsync`); the
dashboard would add more. Before the dashboard, the predicates move into one
internal Infrastructure class (for example `Persistence/ReadRepositories/Visibility.cs`
with `VisibleTasks(context, callerId, role)` and `VisibleProjects(...)`), used by
both read repositories and the dashboard. Behaviour does not change; the
existing task and project tests prove it.

## 3. The numbers

| Field | Meaning |
| --- | --- |
| `TotalProjects` | Count of visible projects |
| `TotalTasks` | Count of visible tasks (all statuses) |
| `TasksByStatus` | `ToDo`, `InProgress`, `Completed`, `Cancelled` counts (all four always present, zero when none) |
| `TasksByPriority` | `Low`, `Medium`, `High`, `Critical` counts (all four always present) |
| `CompletedTasks` / `PendingTasks` | Completed = status `Completed`; Pending = `ToDo` + `InProgress`. `Cancelled` is in neither: it is not done and not waiting **[builder's call]** |
| `OverdueTasks` | Pending tasks with a due date before today **[builder's call]**: one number, because "upcoming" alone hides work that is already late |
| `UpcomingDueTasks` | Up to `Dashboard:UpcomingLimit` pending tasks with a due date from today to today + `Dashboard:UpcomingDays` (inclusive), earliest first, ties by priority (Critical first) then `Id` |

- "Today" is the UTC date from `TimeProvider` (due dates are date only; the
  task module uses the same convention) **[builder's call]**.
- Upcoming list item: `Id`, `Title`, `DueDate`, `Status`, `Priority`,
  `ProjectId`, `ProjectName`, `AssigneeId`, `AssigneeName` (enough for a row
  that links to the task) **[builder's call]**.
- Completed and Cancelled tasks never appear in upcoming or overdue.

## 4. Options

`DashboardOptions` (`SectionName = "Dashboard"`), validated on start
(`ValidateDataAnnotations` + `ValidateOnStart`, like `PagingOptions`):

| Key | Default | Range |
| --- | --- | --- |
| `UpcomingDays` | 7 | 1-90 |
| `UpcomingLimit` | 10 | 1-50 |

Defaults in `src/Api/appsettings.json` **[builder's call]**. No query parameters
on the endpoint: one dashboard, one definition of "upcoming".

## 5. Endpoints

Two endpoints (developer decision 2026-11-11): the summary cards load first and
the upcoming list loads in parallel, without the cost of one request per number.
Fewer, larger requests matter here because the global rate limit is 100 requests
per minute per IP (`RateLimiting:PermitLimit`), every request repeats the token
version check and the visibility query, and numbers that come from one query
always add up (total = sum of the status counts).

| Endpoint | Returns |
| --- | --- |
| `GET /api/dashboard/summary` | `DashboardSummaryDto`: every count (totals, by status, by priority, completed/pending, overdue) |
| `GET /api/dashboard/upcoming` | `IReadOnlyList<UpcomingTaskDto>`: the upcoming due tasks (section 3) |

Both: 200; 401; 503 (`auth.unavailable`), like the other endpoints;
`[Authorize]` (every role has a dashboard, PRD 2). Controller
`DashboardController` in `src/Api/Controllers/Dashboard/`.

DTOs (Application, `Features/Dashboard/`):

- `DashboardSummaryDto(int TotalProjects, int TotalTasks, TaskStatusCountsDto TasksByStatus, TaskPriorityCountsDto TasksByPriority, int CompletedTasks, int PendingTasks, int OverdueTasks)`
- `TaskStatusCountsDto(int ToDo, int InProgress, int Completed, int Cancelled)`,
  `TaskPriorityCountsDto(int Low, int Medium, int High, int Critical)`,
  `UpcomingTaskDto(...)` as section 3.

Use cases: `GetDashboardSummaryQuery() : IQuery<DashboardSummaryDto>` and
`GetUpcomingTasksQuery() : IQuery<IReadOnlyList<UpcomingTaskDto>>`; handlers
inject only `IDashboardReadRepository`, `ISecurityContext`, and (where needed)
`IOptions<DashboardOptions>` and `TimeProvider`; missing `UserId` or `Role` ->
`AuthErrors.TokenRevoked`. `IDashboardReadRepository`:
`GetSummaryAsync(Guid callerId, Role callerRole, DateOnly today, CancellationToken ct)`
and `GetUpcomingAsync(Guid callerId, Role callerRole, DateOnly today, int upcomingDays, int upcomingLimit, CancellationToken ct)`.

## 5a. Queries (Infrastructure)

- Status and priority counts: one `GroupBy` on the visible task set each (SQL
  `GROUP BY`), mapped into the four-field DTOs in memory (at most 4 rows each).
- Totals, completed/pending and overdue come from the status grouping plus one
  `Count` with the overdue predicate; `TotalProjects` is one `Count` on the
  visible project set.
- Upcoming: the visible task set filtered and ordered as section 3,
  `Take(limit)`, hand-written `Select` (assignee name from `Users` ignoring the
  users' soft-delete filter; projects and tasks carry explicit `!IsDeleted`
  because `IgnoreQueryFilters()` applies to the whole query).
- `AsNoTracking`; a few small queries are acceptable (no single-query
  requirement) **[builder's call]**.

## 6. Tests

- Unit: handler passes caller id/role, today (from `FakeTimeProvider`) and
  options to the repository; missing role -> `TokenRevoked`; options validator
  rejects 0 and out-of-range values.
- Integration on the read repository (shared database: each test uses its own
  users and projects, and assertions compare a Team Member's or a fresh PM's
  dashboard, whose visible set contains only the test's own data; Admin numbers
  are checked as deltas or skipped):
  - Team Member: counts only their assigned tasks; unassigned and other members'
    tasks excluded.
  - PM: own projects' tasks plus their assignment in another PM's project.
  - Deleted task and task in a deleted project: not counted, not upcoming.
  - Status and priority counts with all four keys (zeros present).
  - Completed / pending / overdue split; Cancelled in neither.
  - Upcoming: inclusive window edges (today and today + days), beyond the window
    excluded, overdue excluded, Completed/Cancelled excluded, ordering by date
    then priority, limit respected.
- Endpoints: 200 shape of both as JSON, 401 without a token.
- The visibility refactor: existing task and project tests stay green.

## 7. Documents to update with this work

`docs/openapi.json`, `docs/CONTEXT.md` (dashboard built; next: AI endpoint;
build order no longer lists removing Sample before the dashboard), README API
overview, `src/Api/appsettings.json`. PRD 9 is updated when the developer
approves this design (before implementation): the approved details become
CONFIRMED with a change-log row; nothing unapproved is marked CONFIRMED.

## 8. Left to the builder

File and class names beyond those above, how the grouped rows map to the DTOs,
test names, commit order.
