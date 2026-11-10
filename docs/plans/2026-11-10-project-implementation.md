# Project Module Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `Project` aggregate with create / update / change owner / soft delete, details and a server-side list (search, owner filter, sort, paging), the Admin / Project Manager visibility rules, and the "no demotion of a PM who owns projects" guard in the existing role change.

**Architecture:** A Clean Architecture slice copied from Sample (`docs/sample-flow.md`) and the Users feature: `Project`, `ProjectErrors`, `IProjectRepository` in Domain; commands, queries, DTOs, `IProjectReadRepository` and the access helper in Application (identity only from `ISecurityContext`); EF configuration, write and read repositories and the `AddProjects` migration in Infrastructure; `ProjectsController` with named policies in Api.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core SQL Server, FluentValidation, xunit.v3, NSubstitute, Shouldly, Testcontainers. **No new packages.**

**Spec:** `docs/project-design.md` (design, approved 2026-11-10 including every **[builder's call]**), `docs/PRD.md` sections 2, 3 and 8 (requirements). Read the design section named in each task before starting it.

## Global Constraints

- Every rule in `CLAUDE.md` and `.claude/rules/` applies; `docs/CONTEXT.md` working rules 1-10 apply (No-Hallucination Policy, evidence before claims, tests first, stop on surprises).
- No new package. If a task seems to need one, STOP and ask.
- `Project`: `Name` required, trimmed, max 200 (`NameMaxLength`); `Description` optional, trimmed, max 2000 (`DescriptionMaxLength`), empty after trim -> null; `OwnerId` = a User id (reference by id only, no navigation property).
- Error codes exactly: `projects.not_found` (404), `projects.invalid_owner` (400, `ErrorType.Validation`), `projects.owner_not_allowed` (400, `ErrorType.Validation`), `users.owns_projects` (409); concurrency stays `concurrency.conflict` (409).
- A project the caller may not see returns **404 `projects.not_found`**, never 403 (PRD 3), also for update and delete of another PM's project. Ownership is read from the database on every request, never from the token.
- Policies: create, update, delete -> `ProjectManagerOrAdmin`; change owner -> `AdminOnly`; details and list -> `[Authorize]` (a Team Member gets an empty list / 404 until the Task module).
- Sort whitelist `ProjectSortFields`: `name`, `createdAt`, `updatedAt`; default `createdAt`; tie-breaker `Id`. Paging from `PagingOptions` (default 20, max 100, already in `src/Api/appsettings.json`).
- Every controller action declares `[ProducesResponseType]` for every status, including 401 and 503 (`auth.unavailable`), like `UsersController`.
- Handlers are `internal sealed` (Users pattern); query handlers inject only `IProjectReadRepository` and `ISecurityContext`; missing `UserId` or `Role` in the security context -> `AuthErrors.TokenRevoked` (as `ListAssignableUsersQueryHandler`).
- `TimeProvider` everywhere; never `DateTime.UtcNow` in src. No `null!` in src.
- Sample stays (removed after the Task module, design section 1).
- One commit per task, prefix `feat:` / `fix:` / `docs:`, ending with the `Co-Authored-By` line; identity = repository-local git config. Do not push.
- Quick loop: `dotnet build -c Release` (0 warnings) and `dotnet test --solution Datavanced.SmartTaskManagement.slnx -c Release` (a Debug-config UnitTests failure from `tests/WeavingProbe` is pre-existing and unrelated). Docker Desktop must run for integration tests.
- **Stop at each milestone** (end of Tasks 2, 5, 7, 8): run `bash scripts/verify.sh` in Git Bash, show the output, wait for the developer.
- When controllers or DTOs change: `bash scripts/generate-openapi.sh` and commit `docs/openapi.json` in the same task.
- Migration command (after `dotnet tool restore`): `Database__Name=design-time Database__Password=design-time dotnet ef migrations add AddProjects --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations`.

## Review Focus

1. Names and descriptions with surrounding or only whitespace: `"  Alpha  "` is stored as `"Alpha"`; `"   "` as name -> 400; `"   "` as description -> stored as null; a 200-character name wrapped in spaces is accepted (the validator measures the trimmed value, like the aggregate) (Task 1, Task 3).
2. An Admin sending an `ownerId` that does not exist, is `Guid.Empty`, or belongs to an Admin or Team Member: 400 (`projects.invalid_owner`, or a validation error for `Guid.Empty`), never a 500 from the foreign key (Task 3, Task 6).
3. Search text with SQL wildcards (`%`, `_`, `[`) matches literally, not as a pattern (Task 4).
4. A PM using `ownerId=<another PM's id>` in the list filter gets an empty page (200), not another PM's projects and not an error (Task 4).
5. Change owner to the current owner -> 204 without a save; change owner of a soft-deleted or unknown project -> 404 (Task 3, Task 7).

---

## Milestone 1: Domain and persistence

### Task 1: `Project` aggregate, errors, write repository interface

Design: section 2.

**Files:**
- Create: `src/Domain/Projects/Project.cs`, `src/Domain/Projects/ProjectErrors.cs`, `src/Domain/Projects/IProjectRepository.cs`
- Modify: `src/Domain/Users/UserErrors.cs` (add `OwnsProjects`)
- Test: `tests/UnitTests/Domain/Projects/ProjectTests.cs`

**Interfaces:**
- Produces:
  - `sealed class Project : AuditableAggregateRoot<Guid>` with `const int NameMaxLength = 200`, `const int DescriptionMaxLength = 2000`, properties `string Name`, `string? Description`, `Guid OwnerId`
  - `static Project Create(string name, string? description, Guid ownerId)`; `void UpdateDetails(string name, string? description)`; `bool ChangeOwner(Guid newOwnerId)` (false when unchanged); `void Delete(DateTimeOffset deletedAt)` (`MarkDeleted`)
  - `static class ProjectErrors`: `NotFound` (`Error.NotFound("projects.not_found", ...)`), `InvalidOwner` (`Error.Validation("projects.invalid_owner", "The owner must be an existing Project Manager.")`), `OwnerNotAllowed` (`Error.Validation("projects.owner_not_allowed", "A Project Manager can only create projects they own.")`)
  - `UserErrors.OwnsProjects` = `Error.Conflict("users.owns_projects", "The user owns projects; give them to another Project Manager first.")`
  - `interface IProjectRepository`: `Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)` (tracked; soft-deleted excluded), `Task<bool> AnyOwnedByAsync(Guid userId, CancellationToken ct)` (not deleted), `void Add(Project project)`

- [ ] **Step 1: Write the failing tests**

```csharp
private static readonly Guid Owner = Guid.NewGuid();

[Fact] public void Create_ValidInput_TrimsAndKeepsOwner()
{
    var project = Project.Create("  Alpha  ", "  first  ", Owner);
    project.Name.ShouldBe("Alpha");
    project.Description.ShouldBe("first");
    project.OwnerId.ShouldBe(Owner);
    project.Id.ShouldNotBe(Guid.Empty);
}
[Theory, InlineData(null), InlineData(""), InlineData("   ")]
public void Create_DescriptionEmptyAfterTrim_IsNull(string? description) =>
    Project.Create("Alpha", description, Owner).Description.ShouldBeNull();
[Fact] public void Create_NameWithSpacesAroundMaxLength_IsAccepted() =>
    Project.Create($"  {new string('n', 200)}  ", null, Owner).Name.Length.ShouldBe(200);
[Theory, InlineData(""), InlineData("   ")]
public void Create_BlankName_Throws(string name) => Should.Throw<ArgumentException>(() => Project.Create(name, null, Owner));
[Fact] public void Create_NameTooLong_Throws() => Should.Throw<ArgumentException>(() => Project.Create(new string('n', 201), null, Owner));
[Fact] public void Create_DescriptionTooLong_Throws() => Should.Throw<ArgumentException>(() => Project.Create("A", new string('d', 2001), Owner));
[Fact] public void Create_EmptyOwner_Throws() => Should.Throw<ArgumentException>(() => Project.Create("A", null, Guid.Empty));
[Fact] public void UpdateDetails_NewValues_AreTrimmedAndStored()   // UpdateDetails(" Beta ", "   ") -> Name "Beta", Description null
[Fact] public void ChangeOwner_DifferentOwner_ReturnsTrueAndChanges()
[Fact] public void ChangeOwner_SameOwner_ReturnsFalse()
[Fact] public void ChangeOwner_EmptyGuid_Throws()
[Fact] public void Delete_SetsDeletedAt()                           // IsDeleted true, DeletedAt == given time
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test --project tests/UnitTests/UnitTests.csproj -c Release`
Expected: build FAILS, `Project` not found.

- [ ] **Step 3: Implement the files listed above**

Copy the shape of `src/Domain/Samples/Sample.cs` and the trimming of `User.Build`: private constructor with parameter names matching the properties (EF binding), `Guid.CreateVersion7()`, trim first, then `Guard.NotNullOrWhiteSpace` / `Guard.MaxLength` on the trimmed values; an `OwnerId` of `Guid.Empty` throws `ArgumentException` directly (`Guard` has no Guid helper; do not add one for a single use). One private helper normalizes the description for `Create` and `UpdateDetails`.

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test --project tests/UnitTests/UnitTests.csproj -c Release` and `dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj -c Release`
Expected: all PASS (the entity architecture tests check private setters).

- [ ] **Step 5: Commit** `feat: add Project aggregate`

### Task 2: Persistence for projects

Design: section 7.

**Files:**
- Create: `src/Infrastructure/Persistence/Configurations/ProjectConfiguration.cs`, `src/Infrastructure/Persistence/Repositories/ProjectRepository.cs`, migration `AddProjects` (command in Global Constraints)
- Modify: `src/Infrastructure/Configuration/Database/HostApplicationBuilderExtensions.cs` (register `IProjectRepository`)
- Test: `tests/IntegrationTests/Infrastructure/ProjectPersistenceTests.cs` (collection and context creation as `UserPersistenceTests`)

**Interfaces:**
- Consumes: Task 1 types.
- Produces: table `Projects`; `ConfigureAuditable<Project, Guid>()` (audit fields, soft-delete filter, `RowVersion`); `Name` max 200 required; `Description` max 2000; index on `OwnerId`; `builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict)`.

- [ ] **Step 1: Write the failing tests** (users created with `User.Register` + `ChangeRole(Role.ProjectManager)` saved through the context)

```csharp
[Fact] public async Task AddProject_ThenGetById_ReturnsProjectWithAuditFields()     // CreatedAt set by AuditInterceptor, Description round-trips
[Fact] public async Task GetById_SoftDeletedProject_ReturnsNull()
[Fact] public async Task AnyOwnedBy_OnlyDeletedProjects_ReturnsFalse()              // one deleted project -> false; add a live one -> true; other user -> false
[Fact] public async Task AddProject_UnknownOwner_ThrowsDbUpdateException()          // FK Restrict holds
[Fact] public async Task UpdateSameProject_FromTwoContexts_SecondSaveIsConcurrencyConflict()
// load in two AppDbContexts, UpdateDetails in both, UnitOfWork.SaveChangesAsync: first IsSuccess, second Error.Code == "concurrency.conflict"
```

- [ ] **Step 2: Run** `dotnet test --project tests/IntegrationTests/IntegrationTests.csproj -c Release`. Expected: build fails (`ProjectRepository` missing).
- [ ] **Step 3: Implement** the configuration and repository (copy `SampleConfiguration` / `UserRepository`), register the repository, then create the migration. Read the generated migration: it must create only `Projects` with the FK (`ReferentialAction.Restrict`) and the `OwnerId` index. If EF Core logs a model warning about the users' query filter on the required end of the relationship, STOP and show it (UNVERIFIED whether it appears when both sides have a soft-delete filter).
- [ ] **Step 4: Run** integration and architecture tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: persist projects`

**MILESTONE 1: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 2: Application

### Task 3: Project commands (create, update, change owner, delete)

Design: sections 3 and 6.

**Files:**
- Create under `src/Application/Features/Projects/`: `ProjectAccess.cs`; `Commands/CreateProject/{CreateProjectCommand,CreateProjectCommandHandler,CreateProjectCommandValidator}.cs`; `Commands/UpdateProject/{UpdateProjectCommand,UpdateProjectCommandHandler,UpdateProjectCommandValidator}.cs`; `Commands/ChangeProjectOwner/{ChangeProjectOwnerCommand,ChangeProjectOwnerCommandHandler,ChangeProjectOwnerCommandValidator}.cs`; `Commands/DeleteProject/{DeleteProjectCommand,DeleteProjectCommandHandler}.cs`
- Test: `tests/UnitTests/Application/Projects/ProjectCommandHandlerTests.cs`, `tests/UnitTests/Application/Projects/ProjectCommandValidatorTests.cs`

**Interfaces:**
- Consumes: Task 1 types; `IUserRepository.GetByIdAsync` (excludes soft-deleted users); `ISecurityContext`; `IUnitOfWork`; `TimeProvider`.
- Produces:
  - `sealed record CreateProjectCommand(string Name, string? Description, Guid? OwnerId) : ICommand<Guid>`
  - `sealed record UpdateProjectCommand(Guid Id, string Name, string? Description) : ICommand`
  - `sealed record ChangeProjectOwnerCommand(Guid Id, Guid OwnerId) : ICommand`
  - `sealed record DeleteProjectCommand(Guid Id) : ICommand`
  - `internal static class ProjectAccess`: `static bool CanManage(Project project, Guid callerId, Role callerRole)` = Admin, or ProjectManager with `project.OwnerId == callerId`
- Rules:
  - Validators: `Name` `NotEmpty` and trimmed length <= `Project.NameMaxLength`; `Description` trimmed length <= `Project.DescriptionMaxLength`; create: `OwnerId` not `Guid.Empty` when set; change owner: `OwnerId` `NotEmpty`.
  - Create, Admin: `OwnerId` null -> `InvalidOwner`; user null or `Role != ProjectManager` -> `InvalidOwner`. Create, PM: `OwnerId` null or equal to caller -> owner = caller; otherwise `OwnerNotAllowed`.
  - Update / delete: project null or `!CanManage` -> `NotFound`.
  - Change owner: project null -> `NotFound`; new owner null or not a PM -> `InvalidOwner`; `ChangeOwner` false -> success without `SaveChangesAsync`.
  - Every handler: `UserId` or `Role` missing -> `AuthErrors.TokenRevoked`.

- [ ] **Step 1: Write the failing tests** (NSubstitute for `IProjectRepository`, `IUserRepository`, `ISecurityContext`, `IUnitOfWork`; `FakeTimeProvider`)

```csharp
[Fact] public async Task Create_AsAdminWithoutOwner_ReturnsInvalidOwner()
[Fact] public async Task Create_AsAdminWithTeamMemberOwner_ReturnsInvalidOwner()
[Fact] public async Task Create_AsAdminWithUnknownOwner_ReturnsInvalidOwner()
[Fact] public async Task Create_AsAdminWithProjectManagerOwner_AddsProjectAndSaves()   // Add received with OwnerId == pm.Id; result.Value == added project's Id
[Fact] public async Task Create_AsProjectManagerWithoutOwner_OwnerIsCaller()
[Fact] public async Task Create_AsProjectManagerWithOwnId_OwnerIsCaller()
[Fact] public async Task Create_AsProjectManagerForAnotherUser_ReturnsOwnerNotAllowedAndAddsNothing()
[Fact] public async Task Update_AsProjectManagerOfAnotherPmsProject_ReturnsNotFoundAndSavesNothing()
[Fact] public async Task Update_AsAdmin_UpdatesAnyProject()
[Fact] public async Task Delete_AsProjectManagerOfAnotherPmsProject_ReturnsNotFound()
[Fact] public async Task Delete_AsOwner_SoftDeletesWithTimeProviderTime()
[Fact] public async Task ChangeOwner_ToNonProjectManager_ReturnsInvalidOwner()
[Fact] public async Task ChangeOwner_ToCurrentOwner_SucceedsWithoutSave()               // SaveChangesAsync DidNotReceive
[Fact] public async Task ChangeOwner_UnknownProject_ReturnsNotFound()
[Fact] public async Task Create_NoUserIdInSecurityContext_ReturnsTokenRevoked()

// validators
[Fact] public void CreateValidator_WhitespaceName_Fails()
[Fact] public void CreateValidator_TwoHundredCharNameWithSpaces_Passes()
[Fact] public void CreateValidator_EmptyGuidOwner_Fails()
[Fact] public void ChangeOwnerValidator_EmptyGuid_Fails()
```

- [ ] **Step 2: Run** `dotnet test --project tests/UnitTests/UnitTests.csproj -c Release`. Expected: build fails.
- [ ] **Step 3: Implement** by copying the Sample command folders; handlers `internal sealed` like `ChangeUserRoleCommandHandler`.
- [ ] **Step 4: Run** unit and architecture tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add project commands`

### Task 4: Project queries and read repository

Design: sections 3, 4 (DTOs) and 5.

**Files:**
- Create under `src/Application/Features/Projects/`: `ProjectListItemDto.cs`, `ProjectDetailsDto.cs` (with `ProjectOwnerDto`), `ProjectSortFields.cs`, `IProjectReadRepository.cs`, `Queries/GetProjectById/{GetProjectByIdQuery,GetProjectByIdQueryHandler}.cs`, `Queries/ListProjects/{ListProjectsQuery,ListProjectsQueryHandler,ListProjectsQueryValidator}.cs`
- Create: `src/Infrastructure/Persistence/ReadRepositories/ProjectReadRepository.cs`
- Modify: `src/Infrastructure/Configuration/Database/HostApplicationBuilderExtensions.cs` (register `IProjectReadRepository`)
- Test: `tests/UnitTests/Application/Projects/ProjectQueryHandlerTests.cs`, `tests/IntegrationTests/Infrastructure/ProjectReadRepositoryTests.cs`

**Interfaces:**
- Consumes: Task 1-2 types; `QueryableExtensions.ApplySearch` / `ApplySort` / `ToPagedResultAsync`; `ListQuery`, `ListQueryValidator<T>`.
- Produces:
  - `sealed record ProjectListItemDto(Guid Id, string Name, Guid OwnerId, string OwnerName, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)`
  - `sealed record ProjectDetailsDto(Guid Id, string Name, string? Description, ProjectOwnerDto Owner, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt)`; `sealed record ProjectOwnerDto(Guid Id, string FullName, string Email)`
  - `static class ProjectSortFields`: `Name = "name"`, `CreatedAt = "createdAt"`, `UpdatedAt = "updatedAt"`, `All`
  - `sealed record GetProjectByIdQuery(Guid Id) : IQuery<ProjectDetailsDto>`
  - `sealed record ListProjectsQuery(string? Search, Guid? OwnerId, string? SortBy, SortDirection SortDirection, int Page, int PageSize) : ListQuery(...), IQuery<PagedResult<ProjectListItemDto>>`; `ListProjectsQueryValidator(IOptions<PagingOptions>) : ListQueryValidator<ListProjectsQuery>(options, ProjectSortFields.All)`
  - `interface IProjectReadRepository`: `Task<ProjectDetailsDto?> GetByIdAsync(Guid id, Guid callerId, Role callerRole, CancellationToken ct)`, `Task<PagedResult<ProjectListItemDto>> ListAsync(ListProjectsQuery query, Guid callerId, Role callerRole, CancellationToken ct)`
- Read repository: one private `Visible(Guid callerId, Role callerRole)` filter on `IQueryable<Project>` (Admin: all; ProjectManager: `OwnerId == callerId`; anything else: none — the Task module widens this). Then owner filter, `ApplySearch(term, p => p.Name, p => p.Description)`, `ApplySort` with a whitelist map on `Project` (default `CreatedAt`, tie-breaker `Id`), `ToPagedResultAsync` with a projection that reads the owner from `context.Set<User>().IgnoreQueryFilters()` by `OwnerId` (built inside the method; project the email the way `UserReadRepository.ToDto` does). If EF Core cannot translate this, STOP and show the error.

- [ ] **Step 1: Write the failing tests**

```csharp
// Unit (handlers pass the caller from ISecurityContext)
[Fact] public async Task GetById_NotVisible_ReturnsNotFound()                      // read repo returns null -> ProjectErrors.NotFound
[Fact] public async Task List_PassesCallerIdAndRoleToRepository()
[Fact] public async Task List_NoRoleInSecurityContext_ReturnsTokenRevoked()
// validator: ListProjectsQueryValidator sortBy "updatedAt" passes, "password" fails

// Integration on ProjectReadRepository (SQL Server collection; unique marker per test in names)
[Fact] public async Task List_AsProjectManager_ReturnsOnlyOwnProjects()
[Fact] public async Task List_AsProjectManagerFilteringByAnotherPm_ReturnsEmptyPage()
[Fact] public async Task List_AsAdminWithOwnerFilter_ReturnsThatOwnersProjects()
[Fact] public async Task List_AsTeamMember_ReturnsEmptyPage()
[Fact] public async Task List_SoftDeletedProject_IsAbsentForAdmin()
[Fact] public async Task List_SearchMatchesNameOrDescription()
[Fact] public async Task List_SearchWithWildcards_MatchesLiterally()                // "50%_off[1]" finds only the project containing that text
[Theory, InlineData("name"), InlineData("createdAt"), InlineData("updatedAt")]
public async Task List_SortByEachKey_OrdersAscendingAndDescending(string sortBy)
[Fact] public async Task List_PageTwo_ReturnsRemainingItemsAndTotalCount()          // 3 items, pageSize 2 -> page 2 has 1 item, TotalCount 3
[Fact] public async Task GetById_AsAdmin_ReturnsOwnerNameAndEmail()
[Fact] public async Task GetById_AsOtherProjectManager_ReturnsNull()
[Fact] public async Task GetById_OwnerSoftDeleted_StillReturnsOwnerName()           // User has no delete method: set it in the test through context.Entry(user).Property(u => u.IsDeleted).CurrentValue = true
```

- [ ] **Step 2: Run** unit and integration tests. Expected: build fails.
- [ ] **Step 3: Implement**, copying `ListSamples` / `GetSampleById` and `UserReadRepository`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add project queries and read repository`

### Task 5: Demotion guard in the role change

Design: section 6 ("Demotion guard").

**Files:**
- Modify: `src/Application/Features/Users/Commands/ChangeUserRole/ChangeUserRoleCommandHandler.cs` (inject `IProjectRepository`; check after the last-Admin check, before `ChangeRole`)
- Test: `tests/UnitTests/Application/Users/ChangeUserRoleCommandHandlerTests.cs` (add an `IProjectRepository` substitute to `CreateHandler`)

**Interfaces:**
- Consumes: `IProjectRepository.AnyOwnedByAsync`, `UserErrors.OwnsProjects`.
- Rule: user is ProjectManager, new role is not ProjectManager, and `AnyOwnedByAsync(user.Id)` -> `UserErrors.OwnsProjects`; nothing written to Redis or the database.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task ChangeRole_DemotingPmWhoOwnsProjects_ReturnsOwnsProjectsWithoutRedisOrSave()
// SetIfHigherAsync DidNotReceive; SaveChangesAsync DidNotReceive; user.Role still ProjectManager
[Fact] public async Task ChangeRole_DemotingPmWithoutProjects_Succeeds()
[Fact] public async Task ChangeRole_PromotingTeamMember_DoesNotCheckProjects()       // AnyOwnedByAsync DidNotReceive
```

- [ ] **Step 2: Run** unit tests. Expected: FAIL (constructor or behaviour).
- [ ] **Step 3: Implement**; update the handler's `<remarks>` with the known limitation of design section 6 (owner change and demotion at the same moment).
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: refuse demoting a Project Manager who owns projects`

**MILESTONE 2: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 3: Api

### Task 6: `ProjectsController` (create, list, details, update, delete)

Design: sections 3 and 4.

**Files:**
- Create: `src/Api/Controllers/Projects/{ProjectsController,CreateProjectRequest,UpdateProjectRequest,ListProjectsRequest}.cs`, `tests/IntegrationTests/Projects/ProjectsEndpointsTests.cs`
- Modify: `tests/IntegrationTests/Auth/AuthApiFixture.cs` (move `CreateUserAsync` and a `SendAsync` helper from `UsersEndpointsTests` into `AuthApi`), `tests/IntegrationTests/Users/UsersEndpointsTests.cs` (use them), `docs/openapi.json` (regenerated)

**Interfaces:**
- Consumes: Tasks 3-4 commands, queries and DTOs; `AuthorizationPolicies`; `ErrorResults`; `CreatedResponse`.
- Produces:
  - `CreateProjectRequest(string? Name, string? Description, Guid? OwnerId)`, `UpdateProjectRequest(string? Name, string? Description)` (nullable fields, like `CreateSampleRequest`); `ListProjectsRequest` = `ListSamplesRequest` plus `Guid? OwnerId`
  - Routes `api/projects`: `POST` (201 + `Location` + `{ id }`), `GET`, `GET {id:guid}`, `PUT {id:guid}` (204), `DELETE {id:guid}` (204); statuses as design section 4, plus 401 and 503 on every action
  - Test helpers on `AuthApi`: `sealed record TestUser(Guid Id, string Token, string Email, string Cookie)`; `Task<TestUser> CreateUserAsync(Role role, CancellationToken ct, string? marker = null)`; `Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string token, object? body, CancellationToken ct)`. `AuthApi` keeps the `SqlServerFixture` it receives in its constructor.

- [ ] **Step 1: Move the helpers** and run `dotnet test --project tests/IntegrationTests/IntegrationTests.csproj -c Release`. Expected: PASS (refactor only; `UsersEndpointsTests` unchanged in behaviour).
- [ ] **Step 2: Write the failing tests** (collection `AuthCollectionDefinition.Name`)

```csharp
[Fact] public async Task Crud_AsProjectManager_RoundTrip()
// POST -> 201, Location "/api/projects/{id}"; GET {id} -> owner == pm (fullName, email); PUT -> 204, GET shows new name;
// GET list contains it; DELETE -> 204; GET {id} -> 404 projects.not_found; list no longer contains it
[Fact] public async Task Create_WhitespaceName_Returns400WithErrorsName()
[Fact] public async Task Create_AsAdminWithUnknownOwner_Returns400InvalidOwner()     // errorCode "projects.invalid_owner"
[Fact] public async Task Create_AsProjectManagerForAnotherPm_Returns400OwnerNotAllowed()
[Fact] public async Task Create_AsTeamMember_Returns403()
[Fact] public async Task List_AsTeamMember_Returns200EmptyPage()
[Fact] public async Task OtherPmsProject_Returns404OnDetailsUpdateDeleteAndIsNotListed()
[Fact] public async Task DeletedProject_Returns404ForAdminAndIsNotListed()
[Fact] public async Task List_SortByUnknownKey_Returns400()
[Fact] public async Task List_NoToken_Returns401()
```

- [ ] **Step 3: Run** integration tests. Expected: FAIL (404 for the missing routes).
- [ ] **Step 4: Implement** by copying `SamplesController` / `UsersController` (thin controller, `this.ToProblem`, page size default from `PagingOptions`, `request.Name ?? string.Empty`); then `bash scripts/generate-openapi.sh`.
- [ ] **Step 5: Run** all tests. Expected: PASS (architecture test 5: every action has `[Authorize]` or a policy).
- [ ] **Step 6: Commit** `feat: add project endpoints` (include `docs/openapi.json`)

### Task 7: Change-owner endpoint and the demotion rule end to end

Design: sections 3, 4 and 6.

**Files:**
- Create: `src/Api/Controllers/Projects/ChangeProjectOwnerRequest.cs` (`Guid? OwnerId`), `tests/IntegrationTests/Projects/ProjectOwnershipTests.cs`
- Modify: `src/Api/Controllers/Projects/ProjectsController.cs` (`PUT {id:guid}/owner`, `AdminOnly`, missing `OwnerId` -> `ValidationProblem()` as `UsersController.ChangeRole`), `docs/openapi.json` (regenerated)

**Interfaces:**
- Consumes: Task 3 `ChangeProjectOwnerCommand`; Task 5 guard; Task 6 `AuthApi` helpers.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task ChangeOwner_AsAdmin_PreviousOwnerGets404OnNextRequest()   // same PM token: GET {id} 200 before, 404 after; new owner 200
[Fact] public async Task ChangeOwner_ToTeamMember_Returns400InvalidOwner()
[Fact] public async Task ChangeOwner_AsProjectManager_Returns403()
[Fact] public async Task ChangeOwner_ToCurrentOwner_Returns204()
[Fact] public async Task ChangeOwner_DeletedProject_Returns404()
[Fact] public async Task ChangeOwner_MissingOwnerId_Returns400()
[Fact] public async Task DemotePmWhoOwnsProject_Returns409UntilReassigned()
// PUT /api/users/{pm}/role TeamMember -> 409 users.owns_projects; PUT owner to another PM; role change again -> 204
```

- [ ] **Step 2: Run** integration tests. Expected: FAIL.
- [ ] **Step 3: Implement**; `bash scripts/generate-openapi.sh`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add project owner change endpoint` (include `docs/openapi.json`)

**MILESTONE 3: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 4: Documentation

### Task 8: Documentation and final verification

Design: section 9.

**Files:**
- Modify: `docs/project-design.md` (Status: Implemented), `docs/CONTEXT.md` (Project module built; "Not started" and "Build order" updated; test count from the real run), `README.md` (project endpoints in the API overview), `docs/architecture.md` (only if it lists feature modules; check first)

- [ ] **Step 1:** Run the API (`docs/sample-flow.md` section (e)) and in Swagger: log in as the seeded Admin, register a user, promote them to ProjectManager, log in as them, create a project, list it with `search` and `sortBy=updatedAt`, delete it -> 204, GET -> 404. Note any step that did not work and fix the docs.
- [ ] **Step 2:** `bash scripts/verify.sh`. Expected: all steps pass.
- [ ] **Step 3: Commit** `docs: document the Project module`

**MILESTONE 4: show the final verify output; do not push without the developer's go-ahead.**
