# Design: Project module

- Status: Implemented
- Date: 2026-11-10
- Requirements: `../docs/PRD.md` sections 2, 3 and 8 (not repeated here; "PRD 8"
  means PRD section 8). Builds on the auth feature (`docs/auth-design.md`,
  ADRs 0013-0016) and the Sample slice (`docs/sample-flow.md`).
- Marked **[builder's call]**: decided by the agent while writing this design,
  not by the developer.

## 1. Scope

In scope: the `Project` aggregate, create / update / change owner / soft delete,
details, list with search, owner filter, sort and server-side paging, the
visibility rules for Admin and Project Manager, and the "no demotion of a PM who
owns projects" rule in the existing role change.

Out of scope here:

- **Team Member visibility of projects** ("projects where they have at least one
  assigned task", PRD 2) needs tasks. Until the Task module exists, a Team Member
  sees no projects (empty list, 404 on details). The Task module adds the
  assignment-based filter to the project read repository **[builder's call]**.
  Update 2026-11-11: the Task module now delivers it: assignees (any role) see
  the projects of their non-deleted tasks, details only (`docs/task-design.md`).
- Task counts on projects (PRD 8: they come with the Task module). Update
  2026-11-11: delivered by the Task module (`docs/task-design.md`).
- Removing the Sample slice: stays until the Task module is done, then removed
  in its own change following `docs/sample-flow.md` "Remove Sample"
  **[builder's call]** (keeps this change focused; Sample is the copy-source for
  both new modules).

## 2. Domain (src/Domain/Projects)

### `Project` aggregate (`AuditableAggregateRoot<Guid>`, soft delete, RowVersion)

| Field | Rule |
| --- | --- |
| `Name` | Required, trimmed, max 200 (`NameMaxLength`) |
| `Description` | Optional, trimmed, max 2000 (`DescriptionMaxLength`); empty after trim -> null |
| `OwnerId` | Id of a User (reference by id only) |

Behaviour (guard clauses for programmer errors, ddd.md):

- `Project.Create(string name, string? description, Guid ownerId)`
- `UpdateDetails(string name, string? description)`
- `ChangeOwner(Guid newOwnerId)` — returns `bool` (false when unchanged), like
  `User.ChangeRole`.
- `Delete(DateTimeOffset deletedAt)` — soft delete (`MarkDeleted`).

Whether the owner is a Project Manager is checked in the handlers (needs the
user), not in the aggregate.

`ProjectErrors`: `NotFound` (`projects.not_found`, 404), `InvalidOwner`
(`projects.invalid_owner`, 400 — owner missing or not a Project Manager),
`OwnerNotAllowed` (`projects.owner_not_allowed`, 400 — a PM sent an owner other
than themselves).

`IProjectRepository` (Domain): `GetByIdAsync` (tracked, soft-deleted excluded by
the query filter), `Add`, `AnyOwnedByAsync(Guid userId)` (not deleted).

`UserErrors` gains `OwnsProjects` (`users.owns_projects`, 409).

## 3. Access rules (who may do what)

From PRD 2 and PRD 8; enforced in Application handlers using `ISecurityContext`
(`UserId`, `Role`), never trusted from the request body.

| Operation | Admin | Project Manager | Team Member |
| --- | --- | --- | --- |
| Create | `ownerId` required; must be an existing Project Manager | `ownerId` omitted or equal to self; owner = caller | 403 (policy) |
| Details / list | All (not deleted) | Own projects, plus projects where they are assignee of a non-deleted task (those details only, read-only; since 2026-11-11, `docs/task-design.md`) | Projects of their non-deleted tasks, details only (since 2026-11-11, `docs/task-design.md`) |
| Update name/description | Any | Own | 403 (policy) |
| Change owner | Any; new owner must be a Project Manager | 403 (policy) | 403 (policy) |
| Delete (soft) | Any | Own | 403 (policy) |

- Not visible -> **404 `projects.not_found`** (PRD 3), also for update and delete
  of another PM's project (the handler loads the project and treats "not owned"
  as not found).
- Ownership is read from the database on every request (PRD 3), so an owner
  change takes effect on the previous owner's next request.
- Policies: create, update, delete -> `ProjectManagerOrAdmin`; change owner ->
  `AdminOnly`; details and list -> `[Authorize]` (a Team Member gets an empty
  list / 404, not 403, so the Task module can widen it without changing the
  contract) **[builder's call]**.

## 4. Endpoints (src/Api/Controllers/Projects)

| Endpoint | Success | Failures |
| --- | --- | --- |
| `POST /api/projects` body `{ name, description?, ownerId? }` | 201 + `Location` + `{ id }` | 400 validation / `projects.invalid_owner` / `projects.owner_not_allowed`; 403 |
| `GET /api/projects?search=&ownerId=&sortBy=&sortDirection=&page=&pageSize=` | 200 `PagedResult<ProjectListItemDto>` | 400 (bad sort, page, page size) |
| `GET /api/projects/{id}` | 200 `ProjectDetailsDto` | 404 |
| `PUT /api/projects/{id}` body `{ name, description? }` | 204 | 400; 403; 404; 409 `concurrency.conflict` |
| `PUT /api/projects/{id}/owner` body `{ ownerId }` | 204 | 400 `projects.invalid_owner`; 403; 404; 409 |
| `DELETE /api/projects/{id}` | 204 | 403; 404; 409 |

Every action also declares 401 and 503 (`auth.unavailable` from the version
check), like the users endpoints.

DTOs (PRD 8):

- `ProjectListItemDto(Guid Id, string Name, Guid OwnerId, string OwnerName,
  DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)`
- `ProjectDetailsDto(Guid Id, string Name, string? Description,
  ProjectOwnerDto Owner, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)`,
  `ProjectOwnerDto(Guid Id, string FullName, string Email)`

## 5. List query (server-side, PRD 8)

- `ListProjectsQuery(Search, OwnerId?, SortBy, SortDirection, Page, PageSize) :
  ListQuery` + validator (same rules as `ListSamplesQuery`; page size defaults to
  `Paging:DefaultPageSize` = 20 and is capped by `Paging:MaxPageSize` = 100, the
  values already in `src/Api/appsettings.json`, matching PRD 8).
- Search: name and description through the existing `ApplySearch`
  (parameterized, wildcards literal).
- Owner filter: applied inside the caller's visible set, so for a PM it can only
  narrow their own projects (another PM's id returns an empty page) — no special
  error **[builder's call]**.
- Sort whitelist `ProjectSortFields`: `name`, `createdAt`, `updatedAt`; default
  `createdAt`; stable tie-breaker `Id`.
- Read repository `IProjectReadRepository` (Application) /
  `ProjectReadRepository` (Infrastructure): `AsNoTracking`, hand-written `Select`
  joining `Users` for the owner name/email, `ToPagedResultAsync`. Visibility
  filter built from `(callerId, callerRole)` passed by the query handler.
- The owner join ignores the users' soft-delete filter, so a project always
  shows its owner's name even if that user were ever soft-deleted (there is no
  user delete feature today) **[builder's call]**. Owner lookups for create and
  change owner use `IUserRepository.GetByIdAsync`, which excludes soft-deleted
  users, so a deleted user can never become an owner.
- `IgnoreQueryFilters()` in EF Core applies to the whole query, not only to the
  joined set. Because the owner join needs it, `ProjectReadRepository` adds an
  explicit `!p.IsDeleted` for projects. Every set a future read query touches
  (for example tasks in the Task module) must carry its own soft-delete
  predicate whenever `IgnoreQueryFilters()` is used.

## 6. Owner rules in detail

- **Create (Admin):** `ownerId` required; load the user via
  `IUserRepository.GetByIdAsync`; null or `Role != ProjectManager` ->
  `projects.invalid_owner`.
- **Create (PM):** `ownerId` null -> caller; equal to caller -> fine; anything
  else -> `projects.owner_not_allowed`.
- **Change owner (Admin):** same user check as create; unchanged owner -> 204
  with no save.
- **Demotion guard (existing role change, PRD 8):** in
  `ChangeUserRoleCommandHandler`, before `ChangeRole`: if the user is a Project
  Manager, the new role is not Project Manager, and
  `IProjectRepository.AnyOwnedByAsync(user.Id)` -> `UserErrors.OwnsProjects`
  (409). Runs before the Redis write, so nothing changes on refusal.
- Known limitation **[builder's call]**: an owner change and a demotion of the
  new owner at the same moment can both pass (same per-request nature as the
  last-Admin check); the project would then be owned by a non-PM until an Admin
  changes the owner. Documented, not prevented.

## 7. Persistence

- `ProjectConfiguration`: table `Projects`, `ConfigureAuditable<Project, Guid>()`
  (audit fields, soft-delete query filter, RowVersion), `Name` max 200 required,
  `Description` max 2000, index on `OwnerId`, foreign key `OwnerId -> Users.Id`
  with `DeleteBehavior.Restrict` **[builder's call]** (users are never hard
  deleted; the FK keeps owner data consistent).
- Migration `AddProjects`.
- Soft delete hides the project everywhere through the global query filter; the
  Task module will hide tasks of deleted projects the same way (PRD 8).

## 8. Tests

- Unit: `Project` (create guards, update, change owner returns false when
  unchanged, delete); handlers with NSubstitute: Admin create without owner /
  with a Team Member as owner -> `invalid_owner`; PM create for another user ->
  `owner_not_allowed`; PM update / delete of another PM's project -> NotFound;
  change owner to a non-PM -> `invalid_owner`; role change demoting a PM who owns
  a project -> `users.owns_projects` and no Redis write.
- Integration (SQL Server + Redis "Auth" collection pattern): full CRUD round trip
  as PM; Admin creates for a PM and changes owner -> the previous owner gets 404
  on the next request; PM cannot see another PM's project (404 on details,
  update, delete; absent from the list); Team Member gets 403 on create and an
  empty list; soft-deleted project -> 404 and absent from the list for Admin too;
  search on name and description; sort by each whitelisted key and 400 for an
  unknown key; paging (total count, page 2); owner filter for Admin; demotion of
  a PM who owns a project -> 409 until the project is reassigned.
- Architecture tests stay green (new controller has `[Authorize]` / policies on
  every action).

## 9. Documents to update with this work

`docs/openapi.json` (regenerated), `../docs/CONTEXT.md` (Project module built),
README API overview (project endpoints), `docs/architecture.md` if it lists
modules.

## 10. Left to the builder

File and class names beyond those above, validator details, test names
(`Method_Condition_ExpectedResult`), commit order.
