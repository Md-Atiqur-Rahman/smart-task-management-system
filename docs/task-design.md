# Design: Task module

- Status: Implemented (2026-11-11)
- Date: 2026-11-10
- Requirements: `docs/PRD.md` sections 2, 3 and 8 (not repeated here; "PRD 8"
  means PRD section 8). Builds on the Project module (`docs/project-design.md`),
  the auth feature (`docs/auth-design.md`) and the Sample slice
  (`docs/sample-flow.md`).
- Marked **[builder's call]**: decided by the agent while writing this design,
  not by the developer.

## 1. Scope

In scope: the `TaskItem` aggregate; create, edit
details, change status, assign / reassign / unassign, soft delete; task details;
two task lists (per project and across all visible projects) with search,
filters, sort and server-side paging; the assignee visibility rule (PRD 3) for
tasks and projects; task counts on the project list and project details.

Out of scope here:

- Dashboard (PRD 9) and the AI description endpoint (PRD 10): their own changes.
- Removing the Sample slice: its own change right after this one, following
  `docs/sample-flow.md` "Remove Sample" (including moving `CreatedResponse`),
  as decided in `docs/project-design.md` section 1.
- Restoring deleted tasks (PRD 8: no restore feature).

## 2. Domain (src/Domain/Tasks)

### `TaskItem` aggregate (`AuditableAggregateRoot<Guid>`, soft delete, RowVersion)

Named `TaskItem`, with `TaskItemStatus` and `TaskPriority`, so nothing collides
with `System.Threading.Tasks.Task` / `TaskStatus` **[builder's call]**. API
routes and DTO names still say "task".

| Field | Rule |
| --- | --- |
| `ProjectId` | Id of a Project (reference by id only) |
| `Title` | Required, trimmed, max 200 (`TitleMaxLength`) |
| `Description` | Optional, trimmed, max 4000 (`DescriptionMaxLength`); empty after trim -> null |
| `Status` | `TaskItemStatus`: `ToDo`, `InProgress`, `Completed`, `Cancelled`; always `ToDo` on create |
| `Priority` | `TaskPriority`: `Low`, `Medium`, `High`, `Critical`; `Medium` when not sent on create (PRD 8, developer decision) |
| `DueDate` | `DateOnly?`; no range rule (past dates allowed, PRD 8) |
| `AssigneeId` | `Guid?`; Id of a User (reference by id only) |

Behaviour (guard clauses for programmer errors, ddd.md):

- `TaskItem.Create(Guid projectId, string title, string? description, TaskPriority priority, DateOnly? dueDate, Guid? assigneeId)`
- `UpdateDetails(string title, string? description, TaskPriority priority, DateOnly? dueDate)`
- `bool ChangeStatus(TaskItemStatus newStatus)`: any status to any status (PRD
  8, like the default workflows of Azure Boards, Jira and Zoho); false when
  unchanged, so the handler skips the save (204), like `Project.ChangeOwner`.
- `bool Assign(Guid? assigneeId)`: false when unchanged; `null` unassigns.
- `Delete(DateTimeOffset deletedAt)`: soft delete (`MarkDeleted`).

**No state machine.** Status and priority have no transition rules (PRD 8), so
both are plain enums changed through aggregate methods. patterns.md "State
machine — do NOT use when the status is only data with no rules"; a transition
table where every cell is allowed would be a pattern for show. If transition
rules are ever needed, `ChangeStatus` becomes `Result`-returning with a
transition table and a 409 error, without changing the endpoint. Completed and
Cancelled tasks can still be edited, reassigned and deleted.

`TaskErrors`: `NotFound` (`tasks.not_found`, 404), `InvalidAssignee`
(`tasks.invalid_assignee`, 400 — the user does not exist or is soft-deleted),
`AssigneeNotAllowed` (`tasks.assignee_not_allowed`, 400 — a Project Manager
assigned someone other than a Team Member or themselves).

`ITaskItemRepository` (Domain): `GetByIdAsync` (tracked, soft-deleted excluded),
`Add`.

## 3. Access rules (who may do what)

From PRD 2 and PRD 3; enforced in Application handlers with `ISecurityContext`
(`UserId`, `Role`), never trusted from the request body. Ownership and
assignment are read from the database on every request.

Definitions for a caller C and a task T in project P (P not deleted, T not
deleted):

- **Manager of P**: C is an Admin, or C is the Project Manager who owns P.
- **Assignee of T**: `T.AssigneeId == C.UserId`, whatever C's role (PRD 3).
- **C sees T**: manager of P, or assignee of T.
- **C sees P**: Admin; owner of P; or assignee of at least one non-deleted task
  in P ("details only": no edit rights on P).

| Operation | Who | Otherwise |
| --- | --- | --- |
| Create task in P | Manager of P | P not visible -> 404 `projects.not_found`; visible but not manager (an assignee) -> 404 `projects.not_found` **[builder's call]**; Team Member -> 403 (policy) |
| Details of T | C sees T | 404 `tasks.not_found` |
| Edit details of T | Manager of P | 404 `tasks.not_found` (also for an assignee who is not manager); Team Member -> 403 (policy) |
| Change status of T | Manager of P, or assignee of T | 404 `tasks.not_found` |
| Assign / unassign T | Manager of P | 404 `tasks.not_found`; Team Member -> 403 (policy) |
| Delete T | Manager of P | 404 `tasks.not_found`; Team Member -> 403 (policy) |

- Not visible -> 404, never 403 (PRD 3). A user who can see T but may not do the
  operation (an assignee who is not manager, trying to edit) also gets 404, so
  the operation's existence check does not split by role **[builder's call]**.
- A task in a soft-deleted project is treated as not existing (404) for every
  operation, including for Admins (PRD 8).
- Assignment rules (PRD 2): an Admin may assign any existing, not-deleted user;
  a Project Manager (as manager of P) may assign only a Team Member or
  themselves. User missing or soft-deleted -> `tasks.invalid_assignee`; PM
  assigning another PM or an Admin -> `tasks.assignee_not_allowed`. The same
  rules apply to `assigneeId` on create. Unassigning (`null`) is always allowed
  for a manager.
- Unassigning or reassigning removes the previous assignee's access at once
  (their next request gets 404), because visibility is computed per request
  (PRD 3).
- Policies: create, edit details, assign, delete -> `ProjectManagerOrAdmin`;
  details, lists and change status -> `[Authorize]` (an Admin or PM assignee
  also uses the status endpoint) **[builder's call]**.

### Changes to the Project read side (PRD 2, 3, 8)

- `ProjectReadRepository.Visible` widens: Admin all; otherwise
  `OwnerId == caller` **or** an assigned, non-deleted task in the project exists.
  This replaces "Team Member sees nothing" (`docs/project-design.md` section 1).
- `IgnoreQueryFilters()` applies to the whole query (`docs/project-design.md`
  section 5), so every set a project or task read query touches carries its own
  `!IsDeleted` predicate when that call is used.
- The project's own update, delete and owner-change rules do not change: an
  assignee who is not the owner still gets 404 on them.

## 4. Endpoints

| Endpoint | Success | Failures |
| --- | --- | --- |
| `POST /api/projects/{projectId}/tasks` body `{ title, description?, priority?, dueDate?, assigneeId? }` | 201 + `Location: /api/tasks/{id}` + `{ id }` | 400 validation / `tasks.invalid_assignee` / `tasks.assignee_not_allowed`; 403; 404 `projects.not_found` |
| `GET /api/projects/{projectId}/tasks?…` | 200 `PagedResult<TaskListItemDto>` | 400; 404 `projects.not_found` |
| `GET /api/tasks?…` | 200 `PagedResult<TaskListItemDto>` | 400 |
| `GET /api/tasks/{id}` | 200 `TaskDetailsDto` | 404 |
| `PUT /api/tasks/{id}` body `{ title, description?, priority, dueDate? }` | 204 | 400; 403; 404; 409 `concurrency.conflict` |
| `PUT /api/tasks/{id}/status` body `{ status }` | 204 | 400; 404; 409 `concurrency.conflict` |
| `PUT /api/tasks/{id}/assignee` body `{ assigneeId }` (`null` unassigns) | 204 | 400 `tasks.invalid_assignee` / `tasks.assignee_not_allowed`; 403; 404; 409 |
| `DELETE /api/tasks/{id}` | 204 | 403; 404; 409 |

Every action also declares 401 and 503 (`auth.unavailable`), like the project
endpoints. Controller: `TasksController` in `src/Api/Controllers/Tasks/`; the
nested create and list routes live on it with absolute route templates
**[builder's call]**.

`PUT /api/tasks/{id}` replaces all four detail fields: an omitted `priority`
is a 400 (not "keep the old value"), an omitted `dueDate` clears it
**[builder's call]**. Enum values travel as their names (`"InProgress"`,
`"Critical"`), as `Role` does today.

DTOs (PRD 8):

- `TaskListItemDto(Guid Id, string Title, TaskItemStatus Status, TaskPriority Priority, DateOnly? DueDate, Guid? AssigneeId, string? AssigneeName, Guid ProjectId, string ProjectName, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)`
- `TaskDetailsDto(Guid Id, string Title, string? Description, TaskItemStatus Status, TaskPriority Priority, DateOnly? DueDate, TaskAssigneeDto? Assignee, TaskProjectDto Project, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)`
- `TaskAssigneeDto(Guid Id, string FullName, string Email)`, `TaskProjectDto(Guid Id, string Name)`

Project DTO changes (PRD 8 task counts):

- `ProjectListItemDto` gains `int TaskCount, int CompletedTaskCount`.
- `ProjectDetailsDto` gains `ProjectTaskCountsDto TaskCounts` with
  `int ToDo, int InProgress, int Completed, int Cancelled`.
- Counts include only non-deleted tasks the caller may see: all tasks for a
  manager of the project, only their own assigned tasks for a caller who sees
  the project through assignment.

## 5. List queries (server-side, PRD 8)

- `ListTasksQuery(Search, ProjectId?, Status?, Priority?, AssigneeId?, Unassigned, DueFrom?, DueTo?, SortBy, SortDirection, Page, PageSize) : ListQuery` + validator (same paging
  rules as the other lists: default `Paging:DefaultPageSize` 20, max 100;
  `DueFrom <= DueTo` when both are set; `AssigneeId` together with
  `Unassigned = true` is a 400). One query serves both endpoints; the
  project route fills `ProjectId` from the route **[builder's call]**.
- Visibility: the caller's visible task set (section 3) first; filters narrow
  it; a filter can never widen it. On `GET /api/tasks`, a `projectId` filter
  for a project the caller cannot see returns an empty page, not 404, like the
  project owner filter **[builder's call]**. For the project route, the project must be
  visible to the caller, otherwise 404 `projects.not_found`; inside a visible
  project a non-manager sees only their own assigned tasks (PRD 3).
- Search: title and description through the existing `ApplySearch`.
- Filters: `status`, `priority`, `assigneeId`, `unassigned=true` (only tasks
  with no assignee, PRD 8), `dueFrom` / `dueTo` (inclusive, date only; a task
  without a due date never matches a due filter).
- Sort whitelist `TaskSortFields`: `title`, `priority`, `dueDate`, `status`,
  `createdAt`, `updatedAt`; default `createdAt`; stable tie-breaker `Id`.
  - `priority` sorts by rank Low < Medium < High < Critical and `status` by
    ToDo < InProgress < Completed < Cancelled, not alphabetically. Enums are
    stored as strings like `Role` and `SampleStatus` **[builder's call]**, so
    the sort map uses a rank expression that EF Core translates to SQL `CASE`.
    An integration test pins both orders.
  - `dueDate`: tasks without a due date sort last in ascending order and first
    in descending order (plain SQL Server null ordering is not relied on)
    **[builder's call]**.
- Read repository `ITaskItemReadRepository` (Application) /
  `TaskItemReadRepository` (Infrastructure): `AsNoTracking`, hand-written
  `Select`, joining projects (name, not deleted) and users (assignee name and
  email, ignoring the users' soft-delete filter like the project owner join).

## 6. Persistence

- `TaskItemConfiguration`: table `Tasks`, `ConfigureAuditable<TaskItem, Guid>()`,
  `Title` max 200 required, `Description` max 4000, `Status` and `Priority` as
  strings (max 20, like `Role`), `DueDate` as SQL `date`, FK `ProjectId ->
  Projects.Id` and FK `AssigneeId -> Users.Id`, both `DeleteBehavior.Restrict`
  (nothing is hard deleted), indexes on `ProjectId` and `AssigneeId`
  **[builder's call]**.
- UNVERIFIED: EF Core SQL Server maps `DateOnly` to `date` without a converter in
  the EF Core version this repository uses. The builder verifies this against the
  official EF Core docs and the generated migration before relying on it; if a
  converter is needed, stop and report.
- Migration `AddTasks`.
- A task change does not touch the project's `UpdatedAt`; the two aggregates
  are saved independently **[builder's call]**.
- Soft delete of a project hides its tasks without touching the task rows: every
  task read and every task command checks that the project is not deleted
  (section 3).

## 7. Tests

- Unit:
  - `TaskItem`: create guards and trimming, default status `ToDo`; every
    status change to a different status succeeds, including reopening
    `Completed` and `Cancelled` (one theory over the full 4x4 matrix); same
    status returns false; `Assign` returns false when unchanged; `Delete`.
  - Handlers with NSubstitute: create in a project the caller does not manage
    -> `projects.not_found`; PM assigning another PM -> `assignee_not_allowed`;
    unknown assignee -> `invalid_assignee`; Team Member status change on a task
    not assigned to them -> `tasks.not_found`; assignee PM editing details of a
    task in a project they do not own -> `tasks.not_found`; task in a deleted
    project -> `tasks.not_found`.
- Integration (SQL Server + Redis "Auth" collection; every "not listed" check
  filtered so it cannot pass trivially in the shared database):
  - Full round trip as PM: create, details, edit, status ToDo -> InProgress ->
    Completed -> ToDo (reopen), delete -> 404.
  - Team Member: sees only assigned tasks, can change their status, gets 403 on
    create, edit, assign and delete, gets 404 on someone else's task; loses
    access after being unassigned.
  - Admin assigns another PM to a task in PM-A's project: that PM sees the task
    and the project (details only), can change status, and gets 404 on edit.
  - Previous project owner still assigned after an owner change keeps the task.
  - Deleted project -> its tasks are 404 and absent from both lists, also for
    Admin.
  - Search, each filter (including `unassigned=true`, and 400 when combined
    with `assigneeId`), the due-date range, each sort key (priority and status
    in rank order, due dates with nulls last), paging.
  - Project list and details task counts, including a non-manager assignee
    seeing only their own counts and deleted tasks not counted.
  - Project visibility: a Team Member now sees the projects of their tasks in
    the project list and details.
- Architecture tests stay green.

## 8. Documents to update with this work

`docs/openapi.json` (regenerated; project DTOs change too), `docs/CONTEXT.md`
(Task module built; next is removing Sample, then the dashboard; replace the
old proposed "Task status transitions" rule with "any status to any status"),
`docs/project-design.md` (section 1 note: Team Member visibility now delivered
by this module), README API overview (task endpoints).

## 9. Decisions and evidence

Checked on 2026-11-10 in official documentation. "No outside evidence" means
the choice rests on this repository's rules or on reasoning alone.

### Why there is no state machine

The first draft (and `docs/CONTEXT.md`) proposed final statuses: Completed and
Cancelled could not be left. That came from a proposed default, not from
evidence. The developer pointed out that Azure Boards and GitHub let a finished
item be reopened, and the official documentation agrees:

- Azure Boards: inherited processes generate any-to-any state transition rules;
  "A transition from any state to any state is valid". Restrictions are an
  optional custom rule, and Microsoft warns that restricting transitions can stop
  users from fixing mistakes.
  ([rule reference](https://learn.microsoft.com/en-us/azure/devops/organizations/settings/work/rule-reference?view=azure-devops),
  [restrict transitions](https://learn.microsoft.com/en-us/azure/devops/organizations/settings/work/apply-rules-to-workflow-states?view=azure-devops))
- Jira (team-managed): every new status gets an "Any status" transition;
  restricting it is for strict compliance needs.
  ([transitions](https://support.atlassian.com/jira-software-cloud/docs/create-edit-and-delete-transitions-in-team-managed-projects/))
- GitHub Issues: closed issues ("completed" or "not planned") can be reopened
  (`reopenIssue`, state reason `REOPENED`).
  ([Issues GraphQL reference](https://docs.github.com/en/graphql/reference/issues))
- Zoho: Zoho CRM documents task statuses (Not Started, Deferred, In Progress,
  Completed, Waiting on someone else) without transition rules; Zoho Desk
  reopens a task by changing its status from Completed. Zoho Books lets an
  Admin, the assigned user or the creator change a task's status.
  ([Zoho CRM tasks](https://help.zoho.com/portal/en/kb/crm/sales-force-automation/activities/articles/working-with-tasks-and-meetings),
  [Zoho Desk tasks](https://help.zoho.com/portal/en/kb/desk/activities/articles/working-with-tasks-zoho-desk),
  [Zoho Books tasks](https://www.zoho.com/us/books/help/tasks/))

So the developer chose any status to any status (PRD 8). With no transition
rules left, a state machine has nothing to guard: `.claude/rules/patterns.md`
says "State machine — do NOT use when the status is only data with no rules",
and the assignment asks for design patterns "where appropriate". The status is
a plain enum changed through `TaskItem.ChangeStatus`; adding rules later means
a transition table inside that method and a 409 error, with no endpoint change.

### Evidence for the builder's calls

| Decision | Evidence |
| --- | --- |
| Aggregate named `TaskItem`, enums `TaskItemStatus` / `TaskPriority` | .NET Framework Design Guidelines: "DO NOT give types names that would conflict with any type in the Core namespaces" (`System.Threading.Tasks.Task`, `TaskStatus`). ([Names of Namespaces](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces)) |
| A single assignee per task; the assignee may change the status | Zoho Books: one "Assign to" user; status can be changed by Admins, the assigned user or the creator. Zoho CRM: one "Task Owner". ([Zoho Books tasks](https://www.zoho.com/us/books/help/tasks/), [Zoho CRM tasks](https://help.zoho.com/portal/en/kb/crm/sales-force-automation/activities/articles/working-with-tasks-and-meetings)) |
| Default priority `Medium` when none is sent | No outside evidence: Zoho CRM (Highest, High, Normal, Low, Lowest) and Azure Boards (1-4) document the priority values but no default. Decided by the developer (PRD 8): `Medium`, the middle of the four assignment values, rather than making priority required |
| 404 instead of 403 for anything the caller may not see or change, including an assignee trying to edit | GitHub REST API returns 404 instead of 403 "to avoid confirming the existence of private repositories". Also PRD 3. ([GitHub troubleshooting](https://docs.github.com/en/rest/using-the-rest-api/troubleshooting-the-rest-api)) |
| `GET /api/tasks?projectId=` for a project the caller cannot see returns an empty page | Same principle (do not reveal existence) and the same behaviour as the project `ownerId` filter (`docs/project-design.md` section 5). No outside evidence for the exact shape |
| Setting the same status or assignee again: 204, nothing saved | RFC 9110: PUT is idempotent, "the intended effect on the server of multiple identical requests ... is the same as the effect for a single such request". ([RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html)) |
| `PUT /api/tasks/{id}` replaces all four detail fields (omitted `dueDate` clears it; omitted `priority` is 400) | RFC 9110: PUT replaces "all current representations of the target resource with the request content". ([RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html)) |
| Priority and status sort by rank, not alphabetically | Jira orders by priority "according to the importance of the priorities on the global list", not by name. Azure Boards stores priority as an integer (1 highest). ([JQL keywords](https://support.atlassian.com/jira-work-management/docs/advanced-search-reference-jql-keywords), [Azure priority fields](https://learn.microsoft.com/en-us/azure/devops/boards/queries/planning-ranking-priorities?view=azure-devops)). Status rank order: no outside evidence; it follows the natural work order |
| Tasks without a due date sort last (ascending) | No outside evidence found (Jira's documentation does not state where empty values sort). Reasoning: a list sorted by due date is read for "what is due next", so undated tasks should not come first |
| A task change does not touch the project's `UpdatedAt` | Microsoft's tactical DDD guidance: the aggregate is the transactional boundary; Evans and Vernon advocate "one transaction = one aggregate". ([Tactical DDD](https://learn.microsoft.com/en-us/azure/architecture/microservices/model/tactical-domain-driven-design), [Domain events](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)) |
| Indexes on `ProjectId` and `AssigneeId` | SQL Server: "Foreign key constraints don't automatically generate an index"; an index on the foreign key columns lets the engine find related rows quickly. ([table_constraint](https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-table-table-constraint-transact-sql?view=sql-server-ver17), [PK and FK constraints](https://learn.microsoft.com/en-us/sql/relational-databases/tables/primary-and-foreign-key-constraints?view=sql-server-ver16)) |
| Foreign keys with `Restrict` | No outside evidence; consistency with `Projects.OwnerId` (`docs/project-design.md` section 7): rows are only soft deleted, so a cascade would never run and `Restrict` protects against accidental hard deletes |
| Status and priority stored as strings | No outside evidence; consistency with `Role` and `SampleStatus` in this repository (readable rows); sorting by rank is handled in the query |
| Policies: status endpoint `[Authorize]`, the rest `ProjectManagerOrAdmin` | PRD 2 and 3 (an Admin or PM assignee also changes status); no outside evidence needed |

## 10. Left to the builder

File and class names beyond those above, validator details, the rank
expression for enum sorting, test
names (`Method_Condition_ExpectedResult`), commit order.
