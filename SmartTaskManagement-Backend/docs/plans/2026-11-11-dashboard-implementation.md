# Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `GET /api/dashboard/summary` and `GET /api/dashboard/upcoming`, scoped exactly like the task and project lists, computed in the database, with the visibility rule moved into one shared place first.

**Architecture:** A shared Infrastructure visibility helper replaces the four copies in the two read repositories; a Dashboard slice (Application queries, DTOs, `DashboardOptions`, `IDashboardReadRepository`; Infrastructure `DashboardReadRepository`; Api `DashboardController`) builds on it.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core SQL Server, FluentValidation, xunit.v3, NSubstitute, Shouldly, Testcontainers. **No new packages.**

**Spec:** `docs/dashboard-design.md` (approved 2026-11-11, including every **[builder's call]**), `../docs/PRD.md` sections 2, 3 and 9. Read the design section named in each task before starting it.

## Global Constraints

- Every rule in `CLAUDE.md` and `../.claude/rules/` applies; `../docs/CONTEXT.md` working rules apply (No-Hallucination Policy, evidence before claims, tests first, stop on surprises). No new package; no caching of any kind.
- Visibility: tasks = Admin all; owning Project Manager (role ProjectManager and `OwnerId == caller`) all tasks of own projects; anyone: tasks with `AssigneeId == caller`; never deleted tasks or tasks of deleted projects. Projects = Admin all; owner (as above); assignee of a non-deleted task in it; never deleted. One definition, used by the task list, the project list and the dashboard.
- Every query that uses `Users.IgnoreQueryFilters()` carries explicit `!IsDeleted` on every task and project set.
- Numbers: `TasksByStatus` and `TasksByPriority` always carry all four keys (0 when none); Completed = `Completed`; Pending = `ToDo` + `InProgress`; `Cancelled` in neither; Overdue = pending with `DueDate < today`.
- Upcoming: pending tasks with `today <= DueDate <= today + UpcomingDays`, ordered by `DueDate`, then priority rank descending (Critical first), then `Id`; at most `UpcomingLimit`.
- "Today" = `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`; never `DateTime.UtcNow`.
- `DashboardOptions` (`SectionName = "Dashboard"`): `UpcomingDays` default 7, `[Range(1, 90)]`; `UpcomingLimit` default 10, `[Range(1, 50)]`; `ValidateDataAnnotations` + `ValidateOnStart`; values in `src/Api/appsettings.json`.
- Endpoints `[Authorize]`; 200, 401, 503 declared with `[ProducesResponseType]`; handlers `internal sealed`, inject only `IDashboardReadRepository`, `ISecurityContext` and where needed `IOptions<DashboardOptions>`, `TimeProvider`; missing `UserId` or `Role` -> `AuthErrors.TokenRevoked`.
- Integration tests share one database: assert on a Team Member's or a fresh Project Manager's dashboard (visible set = the test's own data); never assert absolute Admin numbers.
- One commit per task, prefix `feat:` / `fix:` / `docs:`, ending with the `Co-Authored-By` line; repository-local identity; do not push.
- Release config for tests: `dotnet test --solution Datavanced.SmartTaskManagement.slnx -c Release`; Docker Desktop must run.
- **Stop at each milestone** (end of Tasks 3 and 5): `bash scripts/verify.sh` in Git Bash, show the output, wait for the developer.
- When controllers or DTOs change: `bash scripts/generate-openapi.sh` and commit `docs/openapi.json` in the same task.

## Review Focus

1. A caller with nothing visible (new Team Member): every count 0, all four status and priority keys present, upcoming empty, never an error (Task 3, Task 4).
2. Window edges: a task due exactly today and exactly today + 7 is upcoming; today + 8 is not; due yesterday is overdue, not upcoming (Task 3).
3. Completed or Cancelled tasks with a due date in the window or in the past: never upcoming, never overdue (Task 3).
4. A task unassigned from a Team Member drops out of their summary and upcoming list at once (Task 3).
5. More than `UpcomingLimit` qualifying tasks: exactly the limit, the earliest ones, ties broken by priority (Task 3).

---

## Milestone 1: Visibility and read side

### Task 1: One visibility definition

Design: section 2 ("One visibility definition").

**Files:**
- Create: `src/Infrastructure/Persistence/ReadRepositories/Visibility.cs`
- Modify: `src/Infrastructure/Persistence/ReadRepositories/TaskItemReadRepository.cs`, `src/Infrastructure/Persistence/ReadRepositories/ProjectReadRepository.cs`

**Interfaces:**
- Produces: `internal static class Visibility`:
  - `static IQueryable<TaskItem> Tasks(AppDbContext context)` (non-deleted), `static IQueryable<Project> Projects(AppDbContext context)` (non-deleted)
  - `static IQueryable<TaskItem> VisibleTasks(AppDbContext context, Guid callerId, Role callerRole)`
  - `static IQueryable<Project> VisibleProjects(AppDbContext context, Guid callerId, Role callerRole)`
  - `static IQueryable<TaskItem> CountedTasks(AppDbContext context, Guid callerId, Role callerRole)` (the current project-count rule)
- Behaviour must not change; `IsProjectVisibleAsync` uses `VisibleProjects(...).AnyAsync(p => p.Id == projectId)`.

- [ ] **Step 1:** Run `dotnet test --project tests/IntegrationTests/IntegrationTests.csproj -c Release` and record the pass count (baseline).
- [ ] **Step 2:** Move the predicates from both repositories into `Visibility` (copy, do not rewrite the rules); replace the private helpers with calls; delete the "keep both identical" comments and put one comment on `Visibility` explaining the explicit `!IsDeleted`.
- [ ] **Step 3:** Run all test projects in Release. Expected: same pass count as Step 1, 0 failed; `dotnet build -c Release` 0 warnings; `dotnet format --verify-no-changes` clean.
- [ ] **Step 4: Commit** `fix: define task and project visibility in one place`

### Task 2: Dashboard Application slice

Design: sections 3, 4 and 5.

**Files:**
- Create under `src/Application/Features/Dashboard/`: `DashboardSummaryDto.cs` (with `TaskStatusCountsDto`, `TaskPriorityCountsDto`), `UpcomingTaskDto.cs`, `DashboardOptions.cs`, `IDashboardReadRepository.cs`, `Queries/GetDashboardSummary/{GetDashboardSummaryQuery,GetDashboardSummaryQueryHandler}.cs`, `Queries/GetUpcomingTasks/{GetUpcomingTasksQuery,GetUpcomingTasksQueryHandler}.cs`
- Create: `src/Application/Configuration/Dashboard/HostApplicationBuilderExtensions.cs` (`ConfigureDashboard`, called from `ConfigureApplication`, pattern of `Configuration/Paging`)
- Test: `tests/UnitTests/Application/Dashboard/DashboardQueryHandlerTests.cs`, `tests/UnitTests/Application/Dashboard/DashboardOptionsTests.cs`

**Interfaces:**
- Produces:
  - `sealed record DashboardSummaryDto(int TotalProjects, int TotalTasks, TaskStatusCountsDto TasksByStatus, TaskPriorityCountsDto TasksByPriority, int CompletedTasks, int PendingTasks, int OverdueTasks)`
  - `sealed record TaskStatusCountsDto(int ToDo, int InProgress, int Completed, int Cancelled)`, `sealed record TaskPriorityCountsDto(int Low, int Medium, int High, int Critical)`
  - `sealed record UpcomingTaskDto(Guid Id, string Title, DateOnly DueDate, TaskItemStatus Status, TaskPriority Priority, Guid ProjectId, string ProjectName, Guid? AssigneeId, string? AssigneeName)`
  - `sealed class DashboardOptions` as Global Constraints
  - `interface IDashboardReadRepository`: `Task<DashboardSummaryDto> GetSummaryAsync(Guid callerId, Role callerRole, DateOnly today, CancellationToken ct)`; `Task<IReadOnlyList<UpcomingTaskDto>> GetUpcomingAsync(Guid callerId, Role callerRole, DateOnly today, int upcomingDays, int upcomingLimit, CancellationToken ct)`
  - `sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>`; `sealed record GetUpcomingTasksQuery : IQuery<IReadOnlyList<UpcomingTaskDto>>`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Summary_PassesCallerAndUtcToday()      // FakeTimeProvider 2026-11-11T23:30Z -> today 2026-11-11
[Fact] public async Task Upcoming_PassesOptionsDaysAndLimit()     // options 7/10 reach the repository
[Fact] public async Task Summary_NoRoleInSecurityContext_ReturnsTokenRevoked()
[Fact] public async Task Upcoming_NoUserIdInSecurityContext_ReturnsTokenRevoked()
[Theory, InlineData(0, 10), InlineData(91, 10), InlineData(7, 0), InlineData(7, 51)]
public void Options_OutOfRange_FailValidation(int days, int limit)   // DataAnnotations Validator
```

- [ ] **Step 2: Run** unit tests. Expected: build fails.
- [ ] **Step 3: Implement**, copying `Features/Projects/Queries/*` and `Configuration/Paging`.
- [ ] **Step 4: Run** unit and architecture tests (the host does not start yet without a repository registration; integration tests come in Task 3). Expected: PASS.
- [ ] **Step 5: Commit** `feat: add dashboard queries`

### Task 3: Dashboard read repository

Design: sections 2, 3 and 5a.

**Files:**
- Create: `src/Infrastructure/Persistence/ReadRepositories/DashboardReadRepository.cs`, `src/Infrastructure/Persistence/ReadRepositories/TaskRanks.cs` (`internal static class TaskRanks` with `static readonly Expression<Func<TaskItem, int>> Priority` and `Status`, moved out of `TaskItemReadRepository`'s sort map, which then uses them)
- Modify: `src/Infrastructure/Persistence/ReadRepositories/TaskItemReadRepository.cs` (use `TaskRanks`), `src/Infrastructure/Configuration/Database/HostApplicationBuilderExtensions.cs` (register), `src/Api/appsettings.json` (`"Dashboard": { "UpcomingDays": 7, "UpcomingLimit": 10 }`)
- Test: `tests/IntegrationTests/Infrastructure/DashboardReadRepositoryTests.cs`

**Interfaces:**
- Consumes: `Visibility` (Task 1); Task 2 types.
- Summary: one `GroupBy(t => t.Status)` and one `GroupBy(t => t.Priority)` on `VisibleTasks` (counts in SQL, mapped into the DTOs in memory, missing keys = 0); overdue = `Count` on the pending set with `DueDate < today`; `TotalProjects` = `VisibleProjects(...).CountAsync()`.
- Upcoming: `VisibleTasks` filtered and ordered as Global Constraints, `Take(limit)`, hand-written `Select` (project name from `Visibility.Projects`, assignee name from `Users.IgnoreQueryFilters()`); priority rank via `TaskRanks.Priority` (the task list's existing sort tests must stay green after the move).

- [ ] **Step 1: Write the failing tests** (fresh users and projects per test)

```csharp
[Fact] public async Task Summary_NewTeamMember_AllZeroWithAllKeys()
[Fact] public async Task Summary_TeamMember_CountsOnlyAssignedTasks()            // other member's and unassigned tasks excluded
[Fact] public async Task Summary_ProjectManager_OwnProjectsPlusOwnAssignmentElsewhere()
[Fact] public async Task Summary_DeletedTaskAndTaskInDeletedProject_NotCounted()
[Fact] public async Task Summary_CompletedPendingOverdue_CancelledInNeither()    // ToDo, InProgress, Completed, Cancelled, an overdue ToDo, an overdue Completed
[Fact] public async Task Summary_UnassignedTask_DropsOutForPreviousAssignee()
[Fact] public async Task Upcoming_WindowEdgesInclusive_BeyondExcluded_OverdueExcluded()   // today, today+7 in; today+8, today-1 out
[Fact] public async Task Upcoming_CompletedAndCancelled_Excluded()
[Fact] public async Task Upcoming_OrderedByDateThenPriority_LimitRespected()       // 12 qualifying, limit 10
[Fact] public async Task Upcoming_ReturnsProjectAndAssigneeNames()
```

- [ ] **Step 2: Run** integration tests. Expected: build fails.
- [ ] **Step 3: Implement.** If EF Core cannot translate a grouping or the projection, STOP and report the exception.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add dashboard read repository`

**MILESTONE 1: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 2: Api and documentation

### Task 4: `DashboardController`

Design: section 5.

**Files:**
- Create: `src/Api/Controllers/Dashboard/DashboardController.cs`, `tests/IntegrationTests/Dashboard/DashboardEndpointsTests.cs`
- Modify: `docs/openapi.json` (regenerated)

**Interfaces:**
- Produces: `GET api/dashboard/summary` -> 200 `DashboardSummaryDto`; `GET api/dashboard/upcoming` -> 200 `IReadOnlyList<UpcomingTaskDto>`; both `[Authorize]`, declaring 200/401/503 like `ProjectsController`.

- [ ] **Step 1: Write the failing tests** (collection `AuthCollectionDefinition.Name`, `AuthApi` helpers)

```csharp
[Fact] public async Task Summary_AsNewTeamMember_Returns200WithZerosAndAllKeys()   // JSON has tasksByStatus.toDo == 0 ... and tasksByPriority.critical == 0
[Fact] public async Task Summary_AfterPmCreatesAndAssignsTask_TeamMemberSeesOne()
[Fact] public async Task Upcoming_TaskDueTomorrow_IsListedForAssignee()          // dueDate as "yyyy-MM-dd", status/priority as names
[Fact] public async Task Summary_NoToken_Returns401()
[Fact] public async Task Upcoming_NoToken_Returns401()
```

- [ ] **Step 2: Run** integration tests. Expected: FAIL (404 for missing routes).
- [ ] **Step 3: Implement**, copying `ProjectsController` (thin, `this.ToProblem`); `bash scripts/generate-openapi.sh`.
- [ ] **Step 4: Run** all tests. Expected: PASS (architecture test 5: every action authorized).
- [ ] **Step 5: Commit** `feat: add dashboard endpoints` (include `docs/openapi.json`)

### Task 5: Documentation and final verification

Design: section 7.

**Files:**
- Modify: `docs/dashboard-design.md` (Status: Implemented), `../docs/CONTEXT.md` (Dashboard built; build order: next the Notifications module, then the AI endpoint; the "remove Sample before the dashboard" step no longer listed; real test count), `README.md` (dashboard endpoints in the API overview)

- [ ] **Step 1:** Update the documents; no secrets; no src changes.
- [ ] **Step 2:** Commit `docs: document the Dashboard`, then `bash scripts/verify.sh`. Expected: all steps pass; if the test count in CONTEXT.md differs from the run, fix it in a second commit `docs: correct the test count`.

**MILESTONE 2: show the final verify output; then push and open a PR against `main` (developer merges manually).**
