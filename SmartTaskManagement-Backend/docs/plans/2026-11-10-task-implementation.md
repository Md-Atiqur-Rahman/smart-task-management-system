# Task Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `TaskItem` aggregate with create / edit details / change status (any to any) / assign / soft delete, task details and two task lists (search, filters incl. unassigned and due range, rank-aware sort, server-side paging), the assignee visibility rule for tasks and projects, and task counts on projects.

**Architecture:** A Clean Architecture slice copied from the Project module: `TaskItem`, enums, `TaskErrors`, `ITaskItemRepository` in Domain; commands, queries, DTOs, `ITaskItemReadRepository`, `TaskAccess` and `TaskAssigneeRules` in Application (identity only from `ISecurityContext`); EF configuration, write and read repositories and the `AddTasks` migration in Infrastructure; `TasksController` in Api. The project read side widens its visibility and gains task counts.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core SQL Server, FluentValidation, xunit.v3, NSubstitute, Shouldly, Testcontainers. **No new packages.**

**Spec:** `docs/task-design.md` (approved 2026-11-10, including every **[builder's call]** and section 9 "Decisions and evidence"), `../docs/PRD.md` sections 2, 3 and 8. Read the design section named in each task before starting it.

## Global Constraints

- Every rule in `CLAUDE.md` and `../.claude/rules/` applies; `../docs/CONTEXT.md` working rules 1-10 apply (No-Hallucination Policy, evidence before claims, tests first, stop on surprises). No new package; if one seems needed, STOP and ask.
- Names: `TaskItem`, `TaskItemStatus` (`ToDo`, `InProgress`, `Completed`, `Cancelled`), `TaskPriority` (`Low`, `Medium`, `High`, `Critical`); folders `Domain/Tasks`, `Application/Features/Tasks`, `Api/Controllers/Tasks`. Never a type named `Task` or `TaskStatus`.
- `TaskItem`: `Title` required, trimmed, max 200 (`TitleMaxLength`); `Description` optional, trimmed, max 4000 (`DescriptionMaxLength`), empty after trim -> null; status `ToDo` on create; priority `Medium` when not sent; `DueDate` `DateOnly?`, no range rule; `AssigneeId` `Guid?`; `ProjectId` and `AssigneeId` by id only (no navigation properties).
- **No state machine**: any status to any status; same status or same assignee again -> success without save (204).
- Error codes exactly: `tasks.not_found` (404), `tasks.invalid_assignee` (400, `ErrorType.Validation`), `tasks.assignee_not_allowed` (400, `ErrorType.Validation`); reuse `projects.not_found` (404) for create / project list on an invisible or unmanaged project; concurrency stays `concurrency.conflict` (409).
- Access (design section 3): manager of P = Admin or owning PM (`ProjectAccess.CanManage`); assignee of T = `T.AssigneeId == caller`, any role; C sees T = manager or assignee; C sees P = Admin, owner, or assignee of a non-deleted task in P. Not visible or not allowed -> 404, never 403. A task in a deleted project does not exist for anyone. Ownership and assignment are read from the database on every request.
- Assignment: Admin -> any existing non-deleted user; PM -> a Team Member or themselves; missing user -> `tasks.invalid_assignee`; PM assigning a PM/Admin other than themselves -> `tasks.assignee_not_allowed`; same rules for `assigneeId` on create; `null` unassigns.
- Policies: create, edit details, assign, delete -> `ProjectManagerOrAdmin`; details, both lists, change status -> `[Authorize]`. Every action declares `[ProducesResponseType]` for every status incl. 401 and 503.
- Sort whitelist `TaskSortFields`: `title`, `priority`, `dueDate`, `status`, `createdAt`, `updatedAt`; default `createdAt`; tie-breaker `Id`. Priority and status sort by rank (Low<Medium<High<Critical; ToDo<InProgress<Completed<Cancelled) through a conditional expression (SQL `CASE`); due date sorts with `DueDate ?? DateOnly.MaxValue` (no due date last ascending, first descending).
- Every read query that uses `IgnoreQueryFilters()` (owner/assignee name lookups) adds explicit `!IsDeleted` for every other set it touches (projects AND tasks).
- Handlers `internal sealed`; missing `UserId` or `Role` -> `AuthErrors.TokenRevoked`. Query handlers inject only their read repository and `ISecurityContext`.
- `TimeProvider` everywhere; never `DateTime.UtcNow` in src. No `null!` in src. `Task<T>`, not `ValueTask<T>`.
- Sample stays (removed in its own change after this module).
- One commit per task, prefix `feat:` / `fix:` / `docs:`, ending with the `Co-Authored-By` line; identity = repository-local git config. Do not push.
- Quick loop: `dotnet build -c Release` (0 warnings) and `dotnet test --solution Datavanced.SmartTaskManagement.slnx -c Release` (Debug-config UnitTests crash from `tests/WeavingProbe` is pre-existing). Docker Desktop must run for integration tests.
- Integration tests share one database: every "not listed" / "absent" assertion filters by a per-test owner, project or search marker so it cannot pass trivially.
- **Stop at each milestone** (end of Tasks 2, 5, 7, 8): `bash scripts/verify.sh` in Git Bash, show the output, wait for the developer.
- When controllers or DTOs change: `bash scripts/generate-openapi.sh` and commit `docs/openapi.json` in the same task.
- Migration command (after `dotnet tool restore`): `Database__Name=design-time Database__Password=design-time dotnet ef migrations add AddTasks --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations`.

## Review Focus

1. Enum values that do not exist in a body or query string (`"status": "Done"`, `?priority=Urgent`): 400, never 500 (Task 6, Task 7).
2. A task whose project is soft-deleted: every task operation 404 for everyone incl. Admin, absent from both lists, not counted on projects (Task 3, Task 4, Task 5).
3. Unassigning or reassigning: the previous assignee's next request gets 404 on the task, and the project disappears from their project list when it was their only task there (Task 7).
4. Conflicting or empty filters: `assigneeId` with `unassigned=true` -> 400; `dueFrom` after `dueTo` -> 400; a task with no due date never matches a due filter (Task 4).
5. Previous project owner after an owner change: still sees and can change status of tasks assigned to them, gets 404 on editing them (Task 7).

---

## Milestone 1: Domain and persistence

### Task 1: `TaskItem` aggregate, enums, errors, write repository interface

Design: section 2.

**Files:**
- Create: `src/Domain/Tasks/{TaskItem,TaskItemStatus,TaskPriority,TaskErrors,ITaskItemRepository}.cs`
- Test: `tests/UnitTests/Domain/Tasks/TaskItemTests.cs`

**Interfaces:**
- Produces:
  - `enum TaskItemStatus { ToDo = 0, InProgress = 1, Completed = 2, Cancelled = 3 }`, `enum TaskPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }`
  - `sealed class TaskItem : AuditableAggregateRoot<Guid>`: `const int TitleMaxLength = 200`, `const int DescriptionMaxLength = 4000`; `Guid ProjectId`, `string Title`, `string? Description`, `TaskItemStatus Status`, `TaskPriority Priority`, `DateOnly? DueDate`, `Guid? AssigneeId`
  - `static TaskItem Create(Guid projectId, string title, string? description, TaskPriority priority, DateOnly? dueDate, Guid? assigneeId)` (status `ToDo`; `Guid.Empty` project or assignee throws `ArgumentException`)
  - `void UpdateDetails(string title, string? description, TaskPriority priority, DateOnly? dueDate)`; `bool ChangeStatus(TaskItemStatus newStatus)`; `bool Assign(Guid? assigneeId)` (`Guid.Empty` throws); `void Delete(DateTimeOffset deletedAt)`
  - `static class TaskErrors`: `NotFound` (`Error.NotFound("tasks.not_found", ...)`), `InvalidAssignee` (`Error.Validation("tasks.invalid_assignee", "The assignee must be an existing user.")`), `AssigneeNotAllowed` (`Error.Validation("tasks.assignee_not_allowed", "A Project Manager can assign only Team Members or themselves.")`)
  - `interface ITaskItemRepository`: `Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct)` (tracked; soft-deleted excluded), `void Add(TaskItem task)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Create_ValidInput_TrimsAndStartsInToDo()        // "  Fix login  " -> "Fix login"; Status ToDo; Id != Guid.Empty
[Theory, InlineData(null), InlineData(""), InlineData("   ")]
public void Create_DescriptionEmptyAfterTrim_IsNull(string? d)
[Fact] public void Create_TitleWithSpacesAroundMaxLength_IsAccepted() // $"  {new string('t', TaskItem.TitleMaxLength)}  "
[Theory, InlineData(""), InlineData("   ")] public void Create_BlankTitle_Throws(string title)
[Fact] public void Create_TitleTooLong_Throws()                       // 201 chars
[Fact] public void Create_DescriptionTooLong_Throws()                 // 4001 chars
[Fact] public void Create_EmptyProjectId_Throws()
[Fact] public void Create_EmptyAssigneeId_Throws()
[Fact] public void Create_PastDueDate_IsAccepted()                    // new DateOnly(2020, 1, 1)
[Theory, MemberData(nameof(AllStatusPairs))]                          // all 16 (from, to) pairs
public void ChangeStatus_AnyToAny_ChangesWhenDifferent(TaskItemStatus from, TaskItemStatus to)
// reach `from` first; ChangeStatus(to).ShouldBe(from != to); Status.ShouldBe(to)
[Fact] public void Assign_SameAssignee_ReturnsFalse()
[Fact] public void Assign_Null_Unassigns()                            // returns true, AssigneeId null
[Fact] public void UpdateDetails_NewValues_AreTrimmedAndStored()
[Fact] public void Delete_SetsDeletedAt()
```

- [ ] **Step 2: Run** `dotnet test --project tests/UnitTests/UnitTests.csproj -c Release`. Expected: build FAILS (`TaskItem` missing).
- [ ] **Step 3: Implement**, copying `src/Domain/Projects/Project.cs` (private constructor binding by name, `Guid.CreateVersion7()`, trim then `Guard`, shared normalize helpers).
- [ ] **Step 4: Run** unit and architecture tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add TaskItem aggregate`

### Task 2: Persistence for tasks

Design: section 6.

**Files:**
- Create: `src/Infrastructure/Persistence/Configurations/TaskItemConfiguration.cs`, `src/Infrastructure/Persistence/Repositories/TaskItemRepository.cs`, migration `AddTasks`
- Modify: `src/Infrastructure/Configuration/Database/HostApplicationBuilderExtensions.cs` (register `ITaskItemRepository`)
- Test: `tests/IntegrationTests/Infrastructure/TaskItemPersistenceTests.cs` (pattern of `ProjectPersistenceTests`)

**Interfaces:**
- Consumes: Task 1 types.
- Produces: table `Tasks`; `ConfigureAuditable<TaskItem, Guid>()`; `Title` max 200 required; `Description` max 4000; `Status` and `Priority` `HasConversion<string>()` max 20 required; `DueDate` SQL `date`; `HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(Restrict)`; `HasOne<User>().WithMany().HasForeignKey(t => t.AssigneeId).OnDelete(Restrict)`; indexes on `ProjectId` and `AssigneeId`.

- [ ] **Step 0: Verify `DateOnly` mapping (UNVERIFIED in the design).** Read the official EF Core docs for the SQL Server provider (https://learn.microsoft.com/en-us/ef/core/providers/sql-server/ and the EF Core 8 "what's new" page on DateOnly/TimeOnly) and check `Directory.Packages.props` for the EF Core version. Record what was verified (URL + sentence) in the report. After Step 3, the generated migration must show `type: "date"` for `DueDate` without a value converter; if not, STOP and report.
- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task AddTask_ThenGetById_RoundTripsAllFieldsIncludingDueDate()   // DueDate 2026-11-20 comes back equal; Status/Priority round-trip
[Fact] public async Task GetById_SoftDeletedTask_ReturnsNull()
[Fact] public async Task AddTask_UnknownProject_ThrowsDbUpdateException()
[Fact] public async Task AddTask_UnknownAssignee_ThrowsDbUpdateException()
[Fact] public async Task UpdateSameTask_FromTwoContexts_SecondSaveIsConcurrencyConflict()
```

- [ ] **Step 2: Run** integration tests. Expected: build fails.
- [ ] **Step 3: Implement** (copy `ProjectConfiguration` / `ProjectRepository`), register, create the migration. Read it: only `Tasks`, two FKs `Restrict`, two indexes; no change to other tables. Any EF model warning -> STOP and report.
- [ ] **Step 4: Run** integration and architecture tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: persist tasks`

**MILESTONE 1: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 2: Application and project read side

### Task 3: Task commands (create, edit details, change status, assign, delete)

Design: sections 3 and 4.

**Files:**
- Create under `src/Application/Features/Tasks/`: `TaskAccess.cs`, `TaskAssigneeRules.cs`; `Commands/CreateTask/`, `Commands/UpdateTask/`, `Commands/ChangeTaskStatus/`, `Commands/AssignTask/`, `Commands/DeleteTask/` (command, handler, validator where input exists)
- Test: `tests/UnitTests/Application/Tasks/TaskCommandHandlerTests.cs`, `tests/UnitTests/Application/Tasks/TaskCommandValidatorTests.cs`

**Interfaces:**
- Consumes: Task 1 types; `IProjectRepository.GetByIdAsync` (excludes deleted projects); `ProjectAccess.CanManage(Project, Guid, Role)`; `IUserRepository.GetByIdAsync`; `ISecurityContext`; `IUnitOfWork`; `TimeProvider`.
- Produces:
  - `sealed record CreateTaskCommand(Guid ProjectId, string Title, string? Description, TaskPriority? Priority, DateOnly? DueDate, Guid? AssigneeId) : ICommand<Guid>` (null priority -> `Medium`)
  - `sealed record UpdateTaskCommand(Guid Id, string Title, string? Description, TaskPriority Priority, DateOnly? DueDate) : ICommand`
  - `sealed record ChangeTaskStatusCommand(Guid Id, TaskItemStatus Status) : ICommand`
  - `sealed record AssignTaskCommand(Guid Id, Guid? AssigneeId) : ICommand`
  - `sealed record DeleteTaskCommand(Guid Id) : ICommand`
  - `internal static class TaskAccess`: `static bool CanSee(TaskItem task, Project project, Guid callerId, Role callerRole)` = `ProjectAccess.CanManage(project, callerId, callerRole) || task.AssigneeId == callerId`
  - `internal static class TaskAssigneeRules`: `static Task<Error?> CheckAsync(Guid assigneeId, Guid callerId, Role callerRole, IUserRepository users, CancellationToken ct)` (user null -> `InvalidAssignee`; caller not Admin and user is not a TeamMember and not the caller -> `AssigneeNotAllowed`; else null)
- Handler flow for Update / ChangeStatus / Assign / Delete: load task (null -> `TaskErrors.NotFound`); load its project via `IProjectRepository` (null = deleted -> `TaskErrors.NotFound`); ChangeStatus requires `TaskAccess.CanSee`, the others require `ProjectAccess.CanManage`, else `TaskErrors.NotFound`. Create: project null or not `CanManage` -> `ProjectErrors.NotFound`. No-op (`ChangeStatus`/`Assign` false) -> success without save.
- Validators: title `NotEmpty`, trimmed length <= `TaskItem.TitleMaxLength`; description trimmed length <= `DescriptionMaxLength`; enums `IsInEnum()`; ids `NotEmpty`; optional assignee not `Guid.Empty` when set.

- [ ] **Step 1: Write the failing tests** (NSubstitute; `FakeTimeProvider`)

```csharp
[Fact] public async Task Create_AsOwnerPm_AddsTaskWithMediumPriorityWhenNoneSent()
[Fact] public async Task Create_InAnotherPmsProject_ReturnsProjectNotFound()
[Fact] public async Task Create_InDeletedProject_ReturnsProjectNotFound()
[Fact] public async Task Create_PmAssigningAnotherPm_ReturnsAssigneeNotAllowed()
[Fact] public async Task Create_AdminAssigningAnotherPm_Succeeds()
[Fact] public async Task Create_UnknownAssignee_ReturnsInvalidAssignee()
[Fact] public async Task Update_AssigneePmNotManager_ReturnsNotFound()
[Fact] public async Task Update_TaskInDeletedProject_ReturnsNotFound()
[Fact] public async Task ChangeStatus_AssignedTeamMember_Succeeds()
[Fact] public async Task ChangeStatus_TeamMemberNotAssigned_ReturnsNotFound()
[Fact] public async Task ChangeStatus_CompletedToToDo_Succeeds()          // reopen allowed
[Fact] public async Task ChangeStatus_SameStatus_SucceedsWithoutSave()
[Fact] public async Task Assign_Null_UnassignsAndSaves()
[Fact] public async Task Assign_SameAssignee_SucceedsWithoutSave()
[Fact] public async Task Delete_AsOwnerPm_SoftDeletesWithTimeProviderTime()
[Fact] public async Task Delete_AsAssigneeNotManager_ReturnsNotFound()
[Fact] public async Task Create_NoUserIdInSecurityContext_ReturnsTokenRevoked()
// validators: whitespace title fails; 200-char title in spaces passes; undefined enum value fails; Guid.Empty assignee fails
```

- [ ] **Step 2: Run** unit tests. Expected: build fails.
- [ ] **Step 3: Implement**, copying the Project command folders.
- [ ] **Step 4: Run** unit and architecture tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add task commands`

### Task 4: Task queries and read repository

Design: sections 4 (DTOs) and 5.

**Files:**
- Create under `src/Application/Features/Tasks/`: `TaskListItemDto.cs`, `TaskDetailsDto.cs` (with `TaskAssigneeDto`, `TaskProjectDto`), `TaskSortFields.cs`, `ITaskItemReadRepository.cs`, `Queries/TaskListQuery.cs` (shared base + `TaskListQueryValidator<T>`), `Queries/ListTasks/`, `Queries/ListProjectTasks/`, `Queries/GetTaskById/`
- Create: `src/Infrastructure/Persistence/ReadRepositories/TaskItemReadRepository.cs`; modify `HostApplicationBuilderExtensions.cs` (register)
- Test: `tests/UnitTests/Application/Tasks/TaskQueryHandlerTests.cs`, `tests/IntegrationTests/Infrastructure/TaskItemReadRepositoryTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3; `QueryableExtensions`; `ListQuery`, `ListQueryValidator<T>`.
- Produces:
  - DTOs exactly as design section 4 (`TaskListItemDto`, `TaskDetailsDto`, `TaskAssigneeDto`, `TaskProjectDto`).
  - `abstract record TaskListQuery(string? Search, TaskItemStatus? Status, TaskPriority? Priority, Guid? AssigneeId, bool Unassigned, DateOnly? DueFrom, DateOnly? DueTo, string? SortBy, SortDirection SortDirection, int Page, int PageSize) : ListQuery(...)`; `abstract class TaskListQueryValidator<T> : ListQueryValidator<T>` adds: `AssigneeId` with `Unassigned` -> fail; `DueFrom > DueTo` -> fail; enums `IsInEnum()`.
  - `sealed record ListTasksQuery(..., Guid? ProjectId) : TaskListQuery, IQuery<PagedResult<TaskListItemDto>>` (invisible `ProjectId` -> empty page)
  - `sealed record ListProjectTasksQuery(Guid ProjectId, ...) : TaskListQuery, IQuery<PagedResult<TaskListItemDto>>` (project not visible -> `ProjectErrors.NotFound`). Two query types replace the design's "one query" builder's call because the two routes differ on an invisible project (empty page vs 404).
  - `sealed record GetTaskByIdQuery(Guid Id) : IQuery<TaskDetailsDto>`
  - `interface ITaskItemReadRepository`: `Task<TaskDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role callerRole, CancellationToken ct)`; `Task<PagedResult<TaskListItemDto>> ListAsync(TaskListQuery query, Guid? projectId, Guid callerId, Role callerRole, CancellationToken ct)`; `Task<bool> IsProjectVisibleAsync(Guid projectId, Guid callerId, Role callerRole, CancellationToken ct)`
- Read repository: one private `VisibleTasks(callerId, callerRole)` = tasks `!t.IsDeleted` whose project is `!p.IsDeleted`, and (Admin, or project owner == caller, or `t.AssigneeId == caller`). Filters, `ApplySearch(title, description)`, `ApplySort` with rank expressions and `DueDate ?? DateOnly.MaxValue`; projection reads project name from projects and assignee from `Users.IgnoreQueryFilters()` (so add explicit `!IsDeleted` everywhere). `IsProjectVisibleAsync`: project not deleted and (Admin, owner, or assignee of a non-deleted task in it).

- [ ] **Step 1: Write the failing tests**

```csharp
// Unit
[Fact] public async Task GetById_NotVisible_ReturnsNotFound()
[Fact] public async Task ListProjectTasks_ProjectNotVisible_ReturnsProjectNotFound()
[Fact] public async Task ListTasks_PassesCallerAndNullProjectToRepository()
[Fact] public void Validator_AssigneeIdWithUnassigned_Fails()
[Fact] public void Validator_DueFromAfterDueTo_Fails()
[Fact] public void Validator_SortByPriority_Passes_SortByPassword_Fails()
// Integration on TaskItemReadRepository (unique marker per test)
[Fact] public async Task List_AsTeamMember_ReturnsOnlyAssignedTasks()
[Fact] public async Task List_AsOwnerPm_ReturnsAllTasksOfOwnProjectsAndOwnAssignments()
[Fact] public async Task List_InProjectAsNonManagerAssignee_ReturnsOnlyOwnTasks()
[Fact] public async Task List_TaskInDeletedProject_IsAbsentForAdmin()
[Fact] public async Task List_SoftDeletedTask_IsAbsent()
[Fact] public async Task List_Unassigned_ReturnsOnlyTasksWithoutAssignee()
[Fact] public async Task List_DueRange_IsInclusiveAndExcludesTasksWithoutDueDate()
[Fact] public async Task List_SearchMatchesTitleOrDescriptionLiterally()      // includes "50%_off"
[Fact] public async Task List_SortByPriority_UsesRankOrder()                   // Low, Medium, High, Critical asc; reversed desc
[Fact] public async Task List_SortByStatus_UsesRankOrder()
[Fact] public async Task List_SortByDueDate_PutsTasksWithoutDueDateLastAscending()
[Fact] public async Task List_PageTwo_ReturnsRemainingItemsAndTotalCount()
[Fact] public async Task GetById_ReturnsAssigneeAndProject()
[Fact] public async Task GetById_AssigneeSoftDeleted_StillReturnsAssigneeName()
[Fact] public async Task IsProjectVisible_AssigneeOfOneTask_IsTrue_OtherTeamMember_IsFalse()
```

- [ ] **Step 2: Run** unit and integration tests. Expected: build fails.
- [ ] **Step 3: Implement**, copying `ListProjects` / `GetProjectById` / `ProjectReadRepository`. If EF Core cannot translate the rank expressions or the projection, STOP and report the exception.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add task queries and read repository`

### Task 5: Project read side: assignee visibility and task counts

Design: section 3 ("Changes to the Project read side") and section 4 (project DTO changes).

**Files:**
- Modify: `src/Infrastructure/Persistence/ReadRepositories/ProjectReadRepository.cs`, `src/Application/Features/Projects/{ProjectListItemDto,ProjectDetailsDto,IProjectReadRepository}.cs` (doc comments), existing project tests that construct the DTOs, `docs/openapi.json` (regenerated: DTOs change)
- Test: `tests/IntegrationTests/Infrastructure/ProjectReadRepositoryTests.cs` (new cases)

**Interfaces:**
- Produces: `ProjectListItemDto` gains `int TaskCount, int CompletedTaskCount` (appended); `ProjectDetailsDto` gains `ProjectTaskCountsDto TaskCounts` (appended); `sealed record ProjectTaskCountsDto(int ToDo, int InProgress, int Completed, int Cancelled)`.
- `Visible`: Admin all; otherwise `OwnerId == caller` or a non-deleted task in the project has `AssigneeId == caller` (replaces "any other role: none"; the switch on role goes away for non-Admins). Counts: non-deleted tasks of the project; for a caller who is not Admin and not owner, only tasks with `AssigneeId == caller`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task List_AsTeamMemberWithAssignedTask_ReturnsThatProject()
[Fact] public async Task List_AsTeamMemberWhoseOnlyTaskIsDeleted_ReturnsNothing()
[Fact] public async Task GetById_AsPmAssigneeOfAnotherPmsProject_ReturnsProject()
[Fact] public async Task List_AsOwner_CountsAllNonDeletedTasks()                 // 3 tasks, 1 completed, 1 deleted -> TaskCount 2, Completed 1
[Fact] public async Task GetById_AsNonOwnerAssignee_CountsOnlyOwnTasksPerStatus()
```

- [ ] **Step 2: Run** integration tests. Expected: FAIL.
- [ ] **Step 3: Implement**; update the doc comments that say "Team Member sees none"; `bash scripts/generate-openapi.sh`.
- [ ] **Step 4: Run** all tests (existing project endpoint tests must stay green; `List_AsTeamMember_Returns200EmptyPage` still holds for a TM without tasks). Expected: PASS.
- [ ] **Step 5: Commit** `feat: show projects to task assignees and add task counts` (include `docs/openapi.json`)

**MILESTONE 2: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 3: Api

### Task 6: `TasksController` (create, lists, details, edit, delete)

Design: section 4.

**Files:**
- Create: `src/Api/Controllers/Tasks/{TasksController,CreateTaskRequest,UpdateTaskRequest,ListTasksRequest}.cs`, `tests/IntegrationTests/Tasks/TasksEndpointsTests.cs`
- Modify: `docs/openapi.json` (regenerated)

**Interfaces:**
- Consumes: Tasks 3-4; `AuthApi.CreateUserAsync` / `SendAsync` / `TestUser`; `AuthorizationPolicies`; `ErrorResults`; `CreatedResponse` (still in `Controllers/Samples`; see `docs/sample-flow.md` "Remove Sample").
- Produces: routes `POST api/projects/{projectId:guid}/tasks` (201, `Location: /api/tasks/{id}`), `GET api/projects/{projectId:guid}/tasks`, `GET api/tasks`, `GET api/tasks/{id:guid}`, `PUT api/tasks/{id:guid}`, `DELETE api/tasks/{id:guid}`; requests with nullable fields (`CreateTaskRequest(string? Title, string? Description, TaskPriority? Priority, DateOnly? DueDate, Guid? AssigneeId)`, `UpdateTaskRequest(string? Title, string? Description, TaskPriority? Priority, DateOnly? DueDate)` — missing `Priority` -> `ValidationProblem()`), `ListTasksRequest` = `ListProjectsRequest` shape + `Status`, `Priority`, `AssigneeId`, `Unassigned`, `DueFrom`, `DueTo`, `ProjectId`.

- [ ] **Step 0: Verify** that ASP.NET Core 10 binds `DateOnly` from the query string and JSON (official docs: model binding simple types; System.Text.Json DateOnly support). Record the URLs in the report; if unclear, STOP.
- [ ] **Step 1: Write the failing tests** (collection `AuthCollectionDefinition.Name`)

```csharp
[Fact] public async Task Crud_AsOwnerPm_RoundTrip()               // create 201 + Location; GET details; PUT 204; list contains; DELETE 204; GET 404 tasks.not_found
[Fact] public async Task Create_WithoutPriority_IsMedium()
[Fact] public async Task Create_InAnotherPmsProject_Returns404ProjectNotFound()
[Fact] public async Task Create_AsTeamMember_Returns403()
[Fact] public async Task Create_UnknownPriorityName_Returns400()    // "priority": "Urgent"
[Fact] public async Task Update_MissingPriority_Returns400()
[Fact] public async Task ListProjectTasks_InvisibleProject_Returns404()
[Fact] public async Task ListTasks_InvisibleProjectFilter_ReturnsEmptyPage()
[Fact] public async Task ListTasks_AssigneeIdWithUnassigned_Returns400()
[Fact] public async Task ListTasks_UnknownStatusName_Returns400()   // ?status=Done
[Fact] public async Task TaskInDeletedProject_Returns404ForAdminAndIsNotListed()
[Fact] public async Task ListTasks_NoToken_Returns401()
```

- [ ] **Step 2: Run** integration tests. Expected: FAIL.
- [ ] **Step 3: Implement** by copying `ProjectsController`; `bash scripts/generate-openapi.sh`.
- [ ] **Step 4: Run** all tests. Expected: PASS (architecture test 5: every action authorized).
- [ ] **Step 5: Commit** `feat: add task endpoints` (include `docs/openapi.json`)

### Task 7: Status and assignee endpoints, access end to end

Design: sections 3 and 4.

**Files:**
- Create: `src/Api/Controllers/Tasks/{ChangeTaskStatusRequest,AssignTaskRequest}.cs`, `tests/IntegrationTests/Tasks/TaskAccessTests.cs`
- Modify: `TasksController.cs` (`PUT api/tasks/{id:guid}/status` `[Authorize]`, missing status -> `ValidationProblem()`; `PUT api/tasks/{id:guid}/assignee` `ProjectManagerOrAdmin`, body `{ assigneeId }` with `null` allowed), `docs/openapi.json`

**Interfaces:**
- Produces: `ChangeTaskStatusRequest(TaskItemStatus? Status)`, `AssignTaskRequest(Guid? AssigneeId)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task TeamMember_SeesOnlyAssignedTasks_AndCanChangeTheirStatus()
[Fact] public async Task TeamMember_Gets403OnEditAssignDelete_And404OnOthersTask()
[Fact] public async Task Status_CompletedBackToToDo_Returns204()                // reopen
[Fact] public async Task Status_UnknownName_Returns400()
[Fact] public async Task Unassign_PreviousAssigneeGets404AndLosesProjectWhenOnlyTask()
[Fact] public async Task AdminAssignsOtherPm_ThatPmSeesTaskAndProject_ChangesStatus_Gets404OnEdit()
[Fact] public async Task PmAssignsAnotherPm_Returns400AssigneeNotAllowed()
[Fact] public async Task AssignUnknownUser_Returns400InvalidAssignee()
[Fact] public async Task PreviousOwnerAfterOwnerChange_KeepsAssignedTask_Gets404OnEdit()
[Fact] public async Task ProjectList_ShowsTaskCountsForOwnerAndOwnCountsForAssignee()
```

- [ ] **Step 2: Run** integration tests. Expected: FAIL.
- [ ] **Step 3: Implement**; `bash scripts/generate-openapi.sh`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add task status and assignment endpoints` (include `docs/openapi.json`)

**MILESTONE 3: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 4: Documentation

### Task 8: Documentation and final verification

Design: section 8.

**Files:**
- Modify: `docs/task-design.md` (Status: Implemented), `docs/project-design.md` (section 1: Team Member visibility and task counts now delivered by the Task module), `../docs/CONTEXT.md` (Task module built; next: remove Sample, then dashboard; replace the old proposed "Task status transitions" rule with "any status to any status, no state machine"; real test count), `README.md` (task endpoints in the API overview; projects row: Team Member now sees projects of their tasks)

- [ ] **Step 1:** Run the API (user-secrets are set on this machine) and in Swagger: as a PM create a project and a task assigned to a registered Team Member; as that TM list tasks, change status ToDo -> InProgress -> Completed -> ToDo; as the PM unassign; as the TM GET the task -> 404. Note any step that failed and fix the docs (not src) or report.
- [ ] **Step 2:** `bash scripts/verify.sh`. Expected: all steps pass.
- [ ] **Step 3: Commit** `docs: document the Task module`

**MILESTONE 4: show the final verify output; then push and open a PR against `main` (developer merges manually).**
