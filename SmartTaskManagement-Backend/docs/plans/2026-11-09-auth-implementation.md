# Authentication and Authorization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Users, roles, register/login/refresh/logout with rotating refresh tokens in an HttpOnly cookie, an immediate-revocation token version check through Redis, Admin user management, first-Admin seeding, shared rate limiting and forwarded headers.

**Architecture:** Clean Architecture slices copied from Sample (`docs/sample-flow.md`): `User` and `RefreshToken` aggregates in Domain; commands, queries, `ITokenService`, `ITokenVersionStore` and `TokenVersionChecker` in Application; EF Core, JWT signing, Redis (token versions, rate limit counter, health check) and the seeder in Infrastructure; controllers, the version-check middleware, cookie handling, policies, rate limiter and forwarded headers in Api.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core SQL Server, FluentValidation, StackExchange.Redis 3.4.31, Serilog, xunit.v3, NSubstitute, Shouldly, Testcontainers.

**Spec:** `docs/auth-design.md` (design), `../docs/PRD.md` sections 2-7 (requirements), `docs/adr/0013-redis-shared-state.md`. Read the design section named in each task before starting it.

## Global Constraints

- Every rule in `CLAUDE.md` and `../.claude/rules/` applies; `../docs/CONTEXT.md` working rules 1-8 apply (No-Hallucination Policy, evidence before claims, tests first, stop on surprises).
- A package is added only after `dotnet package search <id> --exact-match` (and nuget.org for license and net10.0); version only in `Directory.Packages.props`. Already verified: `StackExchange.Redis` 3.4.31 (MIT, net10.0).
- Redis image: `redis:8.10.3`, pinned once as `REDIS_IMAGE` in `.env.example`.
- Access token 10 minutes; refresh token 7 days; reuse grace 30 seconds (allowed 0-60); token-version Redis key `auth:token-version:<userId>` with a fixed 1-hour expiry; login/register limit 10 per minute per IP.
- Refresh cookie: name `__Secure-refresh_token`, `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`. The refresh token never appears in a response body or a log.
- JWT claims: `sub`, `role` (`Admin` | `ProjectManager` | `TeamMember`), `tv`, `jti`, `iat`, `exp`; claim names only in `SecurityClaimTypes`.
- Error codes exactly as `docs/auth-design.md` section 14.
- New endpoints use `[Authorize]` (or a named policy); `[AllowAnonymous]` only on the four auth actions (Task 9).
- `TimeProvider` everywhere; never `DateTime.UtcNow` in src.
- One commit per task, prefix `feat:` / `fix:` / `docs:`, ending with the `Co-Authored-By` line; identity = repository-local git config.
- Quick loop: `dotnet build -c Release` (0 warnings) and `dotnet test --solution Datavanced.SmartTaskManagement.slnx`. Full check at each milestone: `bash scripts/verify.sh` (Git Bash). Docker Desktop must run for integration tests.
- **Stop at each milestone** (end of Tasks 3, 5, 9, 11, 15), show the verify output and wait for the developer.
- When controllers or DTOs change: `bash scripts/generate-openapi.sh` and commit `docs/openapi.json`.
- Migration command: `Database__Name=design-time Database__Password=design-time dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations` (after `dotnet tool restore`).

## Review Focus

1. Email with different case or surrounding spaces (` Rahim@X.com `): register stores `rahim@x.com`, login with any casing works, a second register with another casing gets 409 `users.email_taken` (Task 1, Task 6).
2. A refresh cookie with garbage content (not base64url, 5000 characters, empty): 401 `auth.invalid_refresh_token`, never 500 (Task 7, Task 9).
3. A bearer token with no `tv` claim or a non-integer `tv` (`"abc"`): 401 `auth.token_revoked`, never 500 (Task 8).
4. The last Admin demoting themselves: 409 `users.last_admin`; with two Admins, an Admin demoting themselves succeeds and their own old token is 401 on the next request (Task 10).
5. Logout with no cookie, an unknown token or an already revoked token: 204 and no reuse detection, other devices stay signed in (Task 7, Task 9).

---

## Milestone 1: Domain and persistence

### Task 1: `Role`, `Email`, `User` aggregate

Design: sections 2 and 7.

**Files:**
- Create: `src/Domain/Users/Role.cs`, `src/Domain/Users/Email.cs`, `src/Domain/Users/User.cs`, `src/Domain/Users/UserErrors.cs`, `src/Domain/Users/IUserRepository.cs`
- Test: `tests/UnitTests/Domain/Users/EmailTests.cs`, `tests/UnitTests/Domain/Users/UserTests.cs`

**Interfaces:**
- Produces:
  - `enum Role { Admin = 0, ProjectManager = 1, TeamMember = 2 }`
  - `sealed class Email : ValueObject` with `const int MaxLength = 256`, `string Value`, `static Email Create(string value)` (trims, lower-cases with `ToLowerInvariant`, throws `ArgumentException` when empty, longer than 256 or not exactly one `@` with text on both sides and a `.` after it)
  - `sealed class User : AuditableAggregateRoot<Guid>` with `const int FullNameMaxLength = 100`, properties `Email Email`, `string FullName`, `string PasswordHash`, `Role Role`, `int TokenVersion`; `static User Register(Email email, string fullName, string passwordHash)`; `static User CreateSeedAdmin(Email email, string fullName, string passwordHash)`; `bool ChangeRole(Role newRole)` (true when changed; increments `TokenVersion` only then; it cannot fail, so it returns `bool`, not `Result`)
  - `static class UserErrors`: `NotFound` (`users.not_found`, NotFound), `EmailTaken` (`users.email_taken`, Conflict), `LastAdmin` (`users.last_admin`, Conflict)
  - `interface IUserRepository`: `Task<User?> GetByIdAsync(Guid id, CancellationToken ct)`, `Task<User?> GetByEmailAsync(Email email, CancellationToken ct)`, `Task<bool> EmailExistsAsync(Email email, CancellationToken ct)`, `Task<int> CountByRoleAsync(Role role, CancellationToken ct)`, `void Add(User user)`

- [ ] **Step 1: Write the failing tests**

```csharp
// EmailTests
[Fact] public void Create_MixedCaseWithSpaces_IsTrimmedAndLowerCased() =>
    Email.Create("  Rahim@Example.COM ").Value.ShouldBe("rahim@example.com");
[Theory, InlineData(""), InlineData("no-at-sign"), InlineData("a@b"), InlineData("@x.com"), InlineData("a@@x.com")]
public void Create_InvalidFormat_Throws(string value) => Should.Throw<ArgumentException>(() => Email.Create(value));
[Fact] public void Create_LongerThanMax_Throws() =>
    Should.Throw<ArgumentException>(() => Email.Create(new string('a', 251) + "@x.com"));
[Fact] public void Equality_SameNormalizedValue_IsEqual() =>
    Email.Create("A@X.com").ShouldBe(Email.Create("a@x.com"));

// UserTests
[Fact] public void Register_ValidInput_IsTeamMemberWithVersion1()
{
    var user = User.Register(Email.Create("a@x.com"), "Rahim", "hash");
    user.Role.ShouldBe(Role.TeamMember);
    user.TokenVersion.ShouldBe(1);
    user.Id.ShouldNotBe(Guid.Empty);
}
[Fact] public void CreateSeedAdmin_ValidInput_IsAdminWithVersion1() =>
    User.CreateSeedAdmin(Email.Create("a@x.com"), "Admin", "hash").Role.ShouldBe(Role.Admin);
[Fact] public void Register_FullNameTooLong_Throws() =>
    Should.Throw<ArgumentException>(() => User.Register(Email.Create("a@x.com"), new string('n', 101), "hash"));
[Fact] public void ChangeRole_DifferentRole_ChangesRoleAndIncrementsVersion()
{
    var user = User.Register(Email.Create("a@x.com"), "Rahim", "hash");
    user.ChangeRole(Role.ProjectManager).ShouldBeTrue();
    user.Role.ShouldBe(Role.ProjectManager);
    user.TokenVersion.ShouldBe(2);
}
[Fact] public void ChangeRole_SameRole_ReturnsFalseAndKeepsVersion()
{
    var user = User.Register(Email.Create("a@x.com"), "Rahim", "hash");
    user.ChangeRole(Role.TeamMember).ShouldBeFalse();
    user.TokenVersion.ShouldBe(1);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test --project tests/UnitTests/UnitTests.csproj`
Expected: build FAILS, `User`, `Email`, `Role` not found.

- [ ] **Step 3: Implement the files listed above**

Copy the shape of `src/Domain/Samples/Sample.cs`: private constructor whose parameter names match the properties (EF binding), `Guid.CreateVersion7()`, `Guard` clauses, private setters. `FullName` is trimmed and guarded (`NotNullOrWhiteSpace`, `MaxLength`).

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test --project tests/UnitTests/UnitTests.csproj` and `dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj`
Expected: all PASS (the entity architecture tests also check private setters).

- [ ] **Step 5: Commit** `feat: add User aggregate with Role and Email`

### Task 2: `RefreshToken` aggregate and auth errors

Design: section 2 (`RefreshToken`), section 5 (refresh grace rule).

**Files:**
- Create: `src/Domain/Auth/RefreshToken.cs`, `src/Domain/Auth/AuthErrors.cs`, `src/Domain/Auth/IRefreshTokenRepository.cs`
- Modify: `src/Domain/Results/ErrorType.cs` (add `Unavailable = 5`)
- Test: `tests/UnitTests/Domain/Auth/RefreshTokenTests.cs`

**Interfaces:**
- Produces:
  - `sealed class RefreshToken : AggregateRoot<Guid>`: `Guid UserId`, `string TokenHash`, `DateTimeOffset CreatedAt`, `DateTimeOffset ExpiresAt`, `DateTimeOffset? RevokedAt`, `Guid? ReplacedById`, `byte[] RowVersion` (same `CA1819` suppression text as `AuditableAggregateRoot`); `const int TokenHashLength = 64`
  - `static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)`
  - `Result Revoke(DateTimeOffset now, Guid? replacedById)` (fails with `AuthErrors.InvalidRefreshToken` when already revoked)
  - `bool IsActive(DateTimeOffset now)` = not revoked and `now < ExpiresAt`
  - `bool IsWithinRotationGrace(DateTimeOffset now, TimeSpan grace)` = revoked by rotation (`ReplacedById` not null) and `now - RevokedAt < grace`
  - `static class AuthErrors`: `InvalidCredentials` (`auth.invalid_credentials`, Unauthorized), `InvalidRefreshToken` (`auth.invalid_refresh_token`, Unauthorized), `RefreshSuperseded` (`auth.refresh_superseded`, Unauthorized), `Unavailable` (`auth.unavailable`, Unavailable)
  - `Error.Unavailable(string code, string description)` factory next to the others
  - `interface IRefreshTokenRepository`: `Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct)`, `Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct)`, `void Add(RefreshToken token)`

- [ ] **Step 1: Write the failing tests**

```csharp
private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
private static RefreshToken NewToken() => RefreshToken.Issue(Guid.NewGuid(), new string('a', 64), Now, TimeSpan.FromDays(7));

[Fact] public void Issue_ValidInput_IsActiveUntilExpiry()
{
    var token = NewToken();
    token.ExpiresAt.ShouldBe(Now.AddDays(7));
    token.IsActive(Now.AddDays(7).AddTicks(-1)).ShouldBeTrue();
    token.IsActive(Now.AddDays(7)).ShouldBeFalse();
}
[Fact] public void Revoke_Twice_SecondFailsWithInvalidRefreshToken()
{
    var token = NewToken();
    token.Revoke(Now, null).IsSuccess.ShouldBeTrue();
    token.Revoke(Now, null).Error.ShouldBe(AuthErrors.InvalidRefreshToken);
}
[Fact] public void IsWithinRotationGrace_RotatedTwentyNineSecondsAgo_IsTrue()
{
    var token = NewToken();
    token.Revoke(Now, Guid.NewGuid());
    token.IsWithinRotationGrace(Now.AddSeconds(29), TimeSpan.FromSeconds(30)).ShouldBeTrue();
    token.IsWithinRotationGrace(Now.AddSeconds(30), TimeSpan.FromSeconds(30)).ShouldBeFalse();
}
[Fact] public void IsWithinRotationGrace_RevokedByLogout_IsFalse()
{
    var token = NewToken();
    token.Revoke(Now, null);
    token.IsWithinRotationGrace(Now.AddSeconds(1), TimeSpan.FromSeconds(30)).ShouldBeFalse();
}
[Fact] public void Issue_HashOfWrongLength_Throws() =>
    Should.Throw<ArgumentException>(() => RefreshToken.Issue(Guid.NewGuid(), "short", Now, TimeSpan.FromDays(7)));
```

- [ ] **Step 2: Run to verify they fail** (`dotnet test --project tests/UnitTests/UnitTests.csproj`; build fails).
- [ ] **Step 3: Implement.** Add `ErrorType.Unavailable` and map it in `src/Api/Errors/ErrorResults.cs` (`StatusCodeFor` -> 503, `TitleFor` -> "The service is temporarily unavailable."); extend `tests/IntegrationTests/Api/ErrorHandlingTests.cs` with an `Unavailable` case expecting 503 if it iterates the enum.
- [ ] **Step 4: Run** unit and integration tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add RefreshToken aggregate and auth errors`

### Task 3: Persistence for users and refresh tokens

Design: section 2.

**Files:**
- Create: `src/Infrastructure/Persistence/Configurations/UserConfiguration.cs`, `src/Infrastructure/Persistence/Configurations/RefreshTokenConfiguration.cs`, `src/Infrastructure/Persistence/Repositories/UserRepository.cs`, `src/Infrastructure/Persistence/Repositories/RefreshTokenRepository.cs`, migration `AddUsersAndRefreshTokens`
- Modify: `src/Infrastructure/Configuration/Database/HostApplicationBuilderExtensions.cs` (register both repositories)
- Test: `tests/IntegrationTests/Infrastructure/UserPersistenceTests.cs`

**Interfaces:**
- Consumes: Task 1 and Task 2 types.
- Produces: tables `Users` (unique index on `Email`; `Role` stored as string, max 20; `Email` via value converter, max 256; `PasswordHash` max 500) and `RefreshTokens` (unique index on `TokenHash` char 64; index on `UserId`; `RowVersion` `IsRowVersion()`). `User` uses `ConfigureAuditable<User, Guid>()`.

- [ ] **Step 1: Write the failing tests** (collection `SqlServerCollectionDefinition.Name`, pattern of `PersistenceTests`)

```csharp
[Fact] public async Task AddUser_ThenGetByEmail_ReturnsUserWithNormalizedEmail()  // GetByEmailAsync(Email.Create("A@X.com")) finds "a@x.com"
[Fact] public async Task AddUser_DuplicateEmail_ThrowsDbUpdateException()       // second Add + SaveChanges -> Should.ThrowAsync<DbUpdateException>
[Fact] public async Task CountByRole_TwoAdminsOneMember_ReturnsTwo()
[Fact] public async Task ListActiveForUser_IgnoresRevokedExpiredAndOtherUsers()   // 1 active, 1 revoked, 1 expired, 1 other user -> Count == 1
[Fact] public async Task RevokeSameToken_FromTwoContexts_SecondSaveIsConcurrencyConflict()
// load the same token in two AppDbContexts, Revoke in both, UnitOfWork.SaveChangesAsync: first IsSuccess, second Error.Code == "concurrency.conflict"
```

- [ ] **Step 2: Run** `dotnet test --project tests/IntegrationTests/IntegrationTests.csproj`. Expected: build fails (repositories missing).
- [ ] **Step 3: Implement** configurations and repositories (copy `SampleConfiguration` / `SampleRepository`), register them, then create the migration with the Global Constraints command.
- [ ] **Step 4: Run** integration tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: persist users and refresh tokens`

**MILESTONE 1: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 2: Tokens and Redis

### Task 4: Access token service and refresh token generator

Design: section 4.

**Files:**
- Create: `src/Application/Abstractions/Security/ITokenService.cs` (with `record AccessToken(string Value, DateTimeOffset ExpiresAt)`), `src/Application/Abstractions/Security/IRefreshTokenGenerator.cs` (with `record GeneratedRefreshToken(string RawToken, string TokenHash)`), `src/Infrastructure/Security/JwtTokenService.cs`, `src/Infrastructure/Security/RefreshTokenGenerator.cs`, `src/Infrastructure/Configuration/Security/JwtOptions.cs` (moved)
- Modify: `src/Application/Abstractions/Security/SecurityClaimTypes.cs` (add `TokenVersion = "tv"`, `TokenId = "jti"`), `src/Api/Extensions/AuthenticationExtensions.cs` (use the moved options; registration of `JwtOptions` moves to `ConfigureSecurity` in Infrastructure), `src/Infrastructure/Configuration/Security/HostApplicationBuilderExtensions.cs`, `src/Api/appsettings.json` (`"AccessTokenMinutes": 10` under `Jwt`), `Directory.Packages.props` and `src/Infrastructure/Infrastructure.csproj` only if Step 0 says so
- Delete: `src/Api/Options/JwtOptions.cs`
- Test: `tests/IntegrationTests/Infrastructure/TokenServiceTests.cs`

**Interfaces:**
- Produces:
  - `ITokenService.AccessToken CreateAccessToken(Guid userId, Role role, int tokenVersion)`
  - `IRefreshTokenGenerator.GeneratedRefreshToken Generate()` (32 bytes `RandomNumberGenerator`, base64url) and `string Hash(string rawToken)` (SHA-256, lower-case hex, 64 chars; any input, including garbage, hashes without throwing)
  - `JwtOptions` gains `[Range(1, 60)] int AccessTokenMinutes`

- [ ] **Step 0: Verify the JWT writing package.** `TestTokens` already uses `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler`. Run `dotnet nuget why src/Api/Api.csproj Microsoft.IdentityModel.JsonWebTokens` and `dotnet package search Microsoft.IdentityModel.JsonWebTokens --exact-match`. If Infrastructure needs a direct reference, add the version the JwtBearer package already resolves (no second version), record it in `Directory.Packages.props` with the verification date. If anything is unclear, STOP and ask.
- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void CreateAccessToken_ValidUser_HasSubRoleTvJtiAndTenMinuteExpiry()
// FakeTimeProvider at 2026-11-09T09:00Z; read with JsonWebTokenHandler: sub == userId, role == "ProjectManager", tv == "3", jti not empty, ExpiresAt == 09:10Z, signature validates with the test key
[Fact] public void Generate_TwoCalls_DifferentRawTokensAnd64CharHashes()
[Fact] public void Hash_SameInput_SameOutput_AndGarbageInputDoesNotThrow()   // Hash(new string('%', 5000)).Length == 64
```

- [ ] **Step 2: Run** integration tests. Expected: build fails.
- [ ] **Step 3: Implement** with `JsonWebTokenHandler`, `TimeProvider`, `IOptions<JwtOptions>`; register both services as singletons in `ConfigureSecurity`.
- [ ] **Step 4: Run** all tests. Expected: PASS (existing `SecurityTests` still pass with the moved options).
- [ ] **Step 5: Commit** `feat: add access token service and refresh token generator`

### Task 5: Redis connection, token version store, health check

Design: section 9; ADR 0013 (forward-only, fixed expiry).

**Files:**
- Create: `src/Infrastructure/Configuration/Redis/RedisOptions.cs`, `src/Infrastructure/Configuration/Redis/HostApplicationBuilderExtensions.cs` (`ConfigureRedis`), `src/Infrastructure/Redis/RedisTokenVersionStore.cs`, `src/Infrastructure/Redis/RedisHealthCheck.cs`, `src/Application/Abstractions/Security/ITokenVersionStore.cs`, `src/Application/Abstractions/Security/SharedStateUnavailableException.cs`, `tests/IntegrationTests/Infrastructure/RedisFixture.cs`
- Modify: `Directory.Packages.props`, `src/Infrastructure/Infrastructure.csproj`, `tests/IntegrationTests/IntegrationTests.csproj`, `src/Infrastructure/Configuration/HostApplicationBuilderExtensions.cs` (call `ConfigureRedis`), `.env.example` (`REDIS_IMAGE=redis:8.10.3`, `REDIS_PORT=6379`, `REDIS_PASSWORD=Dev_Only_Redis_Passw0rd!`), `docker-compose.yml` (service `redis`: image `${REDIS_IMAGE:?...}`, `--requirepass ${REDIS_PASSWORD:?...}`, port `127.0.0.1:${REDIS_PORT}:6379`; api env `Redis__ConnectionString: redis:6379,password=${REDIS_PASSWORD}`), `scripts/generate-openapi.sh` (pass `Redis__ConnectionString` built from `.env.example`), `src/Api/appsettings.json` (`"Redis": { "TokenVersionTtlMinutes": 60, "ConnectTimeoutMs": 2000, "OperationTimeoutMs": 1000 }`), `tests/IntegrationTests/Api/ApiFactory.cs` (placeholder `Redis:ConnectionString` setting)
- Test: `tests/IntegrationTests/Infrastructure/TokenVersionStoreTests.cs`

**Interfaces:**
- Produces:
  - `interface ITokenVersionStore`: `Task<int?> GetAsync(Guid userId, CancellationToken ct)`, `Task SetIfHigherAsync(Guid userId, int version, CancellationToken ct)` (Lua: set when missing or lower; always (re)sets the fixed expiry on a write), `Task RemoveAsync(Guid userId, CancellationToken ct)`; every method throws `SharedStateUnavailableException` when Redis is unreachable or times out (catch only `RedisConnectionException` and `RedisTimeoutException`)
  - `sealed class SharedStateUnavailableException : AppException` (code `shared_state.unavailable`)
  - `RedisOptions` (`SectionName = "Redis"`; `[Required] ConnectionString`; `TokenVersionTtlMinutes`, `ConnectTimeoutMs`, `OperationTimeoutMs` with `[Range]`), validated on start; TLS rule as ADR 0011 (`ssl=false` only in Development/Testing, checked by an `IValidateOptions<RedisOptions>`)
  - One `IConnectionMultiplexer` singleton that connects lazily on first use with `AbortOnConnectFail = false` (the app must start without Redis; OpenAPI generation never connects)
  - Health check named `redis` (PING) added with `AddHealthChecks().AddCheck<RedisHealthCheck>("redis")`
  - `RedisFixture` (collection `Redis`) exposing `string ConnectionString`

- [ ] **Step 0: Verify packages.** `StackExchange.Redis` 3.4.31 is verified. For the test container run `dotnet package search Testcontainers.Redis --exact-match`; use it only if a version matching `Testcontainers.MsSql` 4.16.0 exists with a clear license; otherwise use the generic `ContainerBuilder` from the existing Testcontainers package. Record the choice in the commit message.
- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Get_MissingKey_ReturnsNull()
[Fact] public async Task SetIfHigher_LowerAfterHigher_KeepsHigher()          // set 3, set 2 -> Get == 3
[Fact] public async Task SetIfHigher_Write_SetsFixedExpiry()                   // TTL between 59 and 60 minutes; a Get does not extend it
[Fact] public async Task Remove_ExistingKey_GetReturnsNull()
[Fact] public async Task Get_RedisUnreachable_ThrowsSharedStateUnavailable()  // connection string to a closed port, short timeouts
[Fact] public async Task Health_RedisUnreachable_ReportsUnhealthy()           // RedisHealthCheck.CheckHealthAsync -> Unhealthy
```

- [ ] **Step 2: Run** integration tests. Expected: build fails.
- [ ] **Step 3: Implement** the files above.
- [ ] **Step 4: Run** all tests; `docker compose config` must succeed with a `.env` copied from `.env.example`. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add Redis token version store and health check`

**MILESTONE 2: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 3: Auth use cases and endpoints

### Task 6: Register and login

Design: sections 5 (Register, Login), 7.

**Files:**
- Create in `src/Application/Features/Auth/`: `AuthTokens.cs` (`record AuthTokens(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, CurrentUserDto User)`), `CurrentUserDto.cs` (`record CurrentUserDto(Guid Id, string FullName, string Email, Role Role)`), `RefreshTokenOptions.cs` (`SectionName = "RefreshToken"`, `[Range(1, 90)] LifetimeDays = 7`, `[Range(0, 60)] ReuseGraceSeconds = 30`), `AuthSessionIssuer.cs`, `PasswordRules.cs` (`MinLength = 8`, `MaxLength = 128`), `Commands/RegisterUser/{RegisterUserCommand,RegisterUserCommandHandler,RegisterUserCommandValidator}.cs`, `Commands/Login/{LoginCommand,LoginCommandHandler,LoginCommandValidator}.cs`
- Modify: `src/Application/Configuration/Security/HostApplicationBuilderExtensions.cs` (bind `RefreshTokenOptions` with `ValidateOnStart`, register `AuthSessionIssuer` scoped)
- Test: `tests/UnitTests/Application/Auth/RegisterUserCommandHandlerTests.cs`, `LoginCommandHandlerTests.cs`, `RegisterUserCommandValidatorTests.cs`

**Interfaces:**
- Consumes: Tasks 1, 2, 4, 5.
- Produces:
  - `record RegisterUserCommand(string Email, string FullName, string Password) : ICommand<AuthTokens>`
  - `record LoginCommand(string Email, string Password) : ICommand<AuthTokens>`
  - `internal sealed class AuthSessionIssuer`: `AuthTokens Issue(User user)` (creates and `Add`s a `RefreshToken` with the hash, creates the access token; the caller saves) and `Task PublishTokenVersionAsync(User user, CancellationToken ct)` (`SetIfHigherAsync`; catches `SharedStateUnavailableException` and logs a warning: login and register keep working while Redis is down)

- [ ] **Step 1: Write the failing tests** (NSubstitute for repositories, `IUnitOfWork`, `IPasswordHasher`, `ITokenService`, `IRefreshTokenGenerator`, `ITokenVersionStore`; `FakeTimeProvider`)

```csharp
[Fact] public async Task Register_NewEmail_AddsTeamMemberSavesAndReturnsTokens()   // users.Add called with Role.TeamMember; refreshTokens.Add called once; result.Value.User.Email == "a@x.com"
[Fact] public async Task Register_EmailExistsInOtherCase_ReturnsEmailTaken()       // EmailExistsAsync(Email.Create("A@X.com")) true -> Error == UserErrors.EmailTaken, nothing added
[Fact] public async Task Login_WrongPassword_ReturnsInvalidCredentials()
[Fact] public async Task Login_UnknownEmail_ReturnsInvalidCredentialsAndStillVerifiesAHash()  // hasher.Verify received once
[Fact] public async Task Login_Valid_ReturnsTokensWithCurrentVersionAndPublishesIt()        // CreateAccessToken(id, role, 4); SetIfHigherAsync(id, 4)
[Fact] public async Task Login_RedisDown_StillSucceeds()                           // SetIfHigherAsync throws SharedStateUnavailableException -> IsSuccess
[Theory, InlineData("short1"), InlineData("allletters"), InlineData("12345678")]
public void RegisterValidator_WeakPassword_HasPasswordError(string password)
[Fact] public void RegisterValidator_Password129Chars_HasPasswordError()
```

- [ ] **Step 2: Run** unit tests. Expected: build fails.
- [ ] **Step 3: Implement.** Email validation in validators uses the same rule as `Email.Create` (call it inside a `Must` that catches `ArgumentException`, or share one static `Email.IsValid(string)`); the dummy hash for unknown users is created once per handler type (`static Lazy<string>` filled from `IPasswordHasher.Hash` on first use).
- [ ] **Step 4: Run** unit and architecture tests (`HandlerTests` checks handler dependencies). Expected: PASS.
- [ ] **Step 5: Commit** `feat: add register and login use cases`

### Task 7: Refresh with rotation, reuse detection and grace; logout

Design: section 5 (Refresh, Logout).

**Files:**
- Create in `src/Application/Features/Auth/Commands/`: `RefreshSession/{RefreshSessionCommand,RefreshSessionCommandHandler}.cs`, `Logout/{LogoutCommand,LogoutCommandHandler}.cs`
- Test: `tests/UnitTests/Application/Auth/RefreshSessionCommandHandlerTests.cs`, `LogoutCommandHandlerTests.cs`

**Interfaces:**
- Produces:
  - `record RefreshSessionCommand(string RefreshToken) : ICommand<AuthTokens>`
  - `record LogoutCommand(string? RefreshToken) : ICommand`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Refresh_ActiveToken_RevokesItWithReplacementAndIssuesNewPair()  // old.ReplacedById == new token id; one SaveChanges
[Fact] public async Task Refresh_UnknownOrGarbageToken_ReturnsInvalidRefreshToken()      // RefreshToken = new string('%', 5000)
[Fact] public async Task Refresh_ExpiredToken_ReturnsInvalidRefreshToken()
[Fact] public async Task Refresh_RotatedTenSecondsAgo_ReturnsSupersededAndRevokesNothing()   // ListActiveForUserAsync not called; SaveChanges not called
[Fact] public async Task Refresh_RotatedThirtyOneSecondsAgo_RevokesAllActiveTokensOfUser()   // 2 active tokens -> both RevokedAt set; Error == InvalidRefreshToken
[Fact] public async Task Refresh_RevokedByLogout_IsReuseNotGrace()
[Fact] public async Task Refresh_ConcurrencyConflictOnSave_ReturnsInvalidRefreshToken()
[Fact] public async Task Logout_ActiveToken_RevokesOnlyThatToken()                      // ListActiveForUserAsync never called; user TokenVersion unchanged
[Fact] public async Task Logout_NoTokenUnknownOrAlreadyRevoked_SucceedsWithoutChanges()
```

- [ ] **Step 2: Run** unit tests. Expected: build fails.
- [ ] **Step 3: Implement** following design section 5 steps 1-4; the grace window is `RefreshTokenOptions.ReuseGraceSeconds`; log reuse with user id only.
- [ ] **Step 4: Run** unit tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add refresh rotation with reuse detection and logout`

### Task 8: Token version check middleware

Design: section 5 (Version check), section 6 (`ISecurityContext.Role`); ADR 0014.

**Files:**
- Create: `src/Application/Abstractions/Security/ITokenVersionChecker.cs` (with `enum TokenVersionCheck { Valid, Revoked, Unavailable }`), `src/Application/Abstractions/Security/TokenVersionChecker.cs` (internal), `src/Application/Features/Users/IUserReadRepository.cs` (method `Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken ct)` now; Task 10 adds the list methods), `src/Infrastructure/Persistence/ReadRepositories/UserReadRepository.cs`, `src/Api/Middleware/TokenVersionMiddleware.cs`, `tests/IntegrationTests/Api/TestSupport/InMemoryTokenVersionStore.cs`, `docs/adr/0014-auth-endpoints-and-redis-amendments.md`
- Modify: `src/Api/Extensions/AuthenticationExtensions.cs` (`UseAuthentication` -> `TokenVersionMiddleware` -> `SecurityContextMiddleware` -> `UseAuthorization`), `src/Application/Abstractions/Security/ISecurityContext.cs` and `SecurityContext.cs` (add `Role? Role`, parsed from the single `role` claim with `Enum.TryParse`; keep `Roles`), `tests/IntegrationTests/Api/ApiFactory.cs` (replace `ITokenVersionStore` with `InMemoryTokenVersionStore` by default, expose it as `TokenVersions`; `WithRedis(string connectionString)` keeps the real store), `tests/IntegrationTests/Api/SecurityTests.cs` (tokens carry `tv`; store preseeded), `docs/adr/README.md`
- Test: `tests/UnitTests/Application/Security/TokenVersionCheckerTests.cs`, `tests/IntegrationTests/Api/TokenVersionTests.cs`

**Interfaces:**
- Produces: `ITokenVersionChecker.Task<TokenVersionCheck> CheckAsync(Guid userId, int tokenVersion, CancellationToken ct)` (store hit -> compare; miss -> read repository -> `SetIfHigherAsync` -> compare; unknown user -> `Revoked`; `SharedStateUnavailableException` -> `Unavailable`). Middleware: skips endpoints with `IAllowAnonymous` metadata and unauthenticated requests; missing or non-integer `tv` or `sub` -> 401 `auth.token_revoked`; `Revoked` -> 401 `auth.token_revoked`; `Unavailable` -> 503 `auth.unavailable`; responses are ProblemDetails with `errorCode` and `traceId` (same shape as the rate limiter's `OnRejected`).
- ADR 0014 records: (1) the four anonymous auth actions (used in Task 9), (2) version check in middleware instead of `OnTokenValidated`, (3) role change writes Redis before the database, (4) fixed instead of sliding expiry; status Accepted (developer approved the design on 2026-11-09).

- [ ] **Step 1: Write the failing tests**

```csharp
// Unit
[Fact] public async Task Check_StoreHasSameVersion_Valid()
[Fact] public async Task Check_StoreHasHigherVersion_Revoked()
[Fact] public async Task Check_StoreMiss_LoadsFromDatabaseAndStores()   // read repo returns 2 -> SetIfHigherAsync(id, 2) -> Valid for tv 2
[Fact] public async Task Check_StoreMissAndUnknownUser_Revoked()
[Fact] public async Task Check_StoreThrowsUnavailable_Unavailable()
// Integration (default ApiFactory, /test/whoami)
[Fact] public async Task Whoami_TokenVersionMatches_Returns200()
[Fact] public async Task Whoami_TokenVersionBehind_Returns401TokenRevoked()
[Theory, InlineData(null), InlineData("abc")]
public async Task Whoami_MissingOrNonIntegerTv_Returns401TokenRevoked(string? tv)
[Fact] public async Task Whoami_StoreUnavailable_Returns503AuthUnavailable()   // InMemoryTokenVersionStore.SimulateOutage = true
[Fact] public async Task AnonymousEndpoint_WithStaleToken_StillReturns200()     // /test/anonymous
```

- [ ] **Step 2: Run** unit and integration tests. Expected: build fails.
- [ ] **Step 3: Implement**, update existing `SecurityTests`, write ADR 0014 and its index row.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: check token version on every authenticated request`

### Task 9: Auth endpoints, cookie and origin check

Design: sections 3, 4 (cookie, CSRF), 10 (strict limit is Task 12).

**Files:**
- Create: `src/Api/Controllers/Auth/AuthController.cs`, `src/Api/Controllers/Auth/{RegisterRequest,LoginRequest,AuthResponse}.cs` (`AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User)`), `src/Api/Auth/RefreshTokenCookie.cs` (internal: `Name`, `Path`, `Append(HttpResponse, string rawToken, DateTimeOffset expiresAt)`, `Delete(HttpResponse)`, `string? Read(HttpRequest)`), `src/Api/Auth/RequireAllowedOriginAttribute.cs` (action filter; `Origin` present and not in `Cors:AllowedOrigins` -> 403 `auth.origin_not_allowed`), `src/Application/Features/Auth/Queries/GetCurrentUser/{GetCurrentUserQuery,GetCurrentUserQueryHandler}.cs` (extend `IUserReadRepository` with `Task<CurrentUserDto?> GetCurrentUserAsync(Guid id, CancellationToken ct)`), `tests/IntegrationTests/Auth/AuthApiFixture.cs` (real SQL Server + Redis via `WithDatabase` + `WithRedis`)
- Modify: `src/Api/Extensions/CorsExtensions.cs` (`.AllowCredentials()`), `tests/ArchitectureTests/ControllerAuthorizationTests.cs` (allow-list of exactly `AuthController.Register`, `Login`, `Refresh`, `Logout`; Sample exemption stays until Task 14), `../.claude/rules/security.md`
- Test: `tests/IntegrationTests/Auth/AuthEndpointsTests.cs`

**Interfaces:**
- Produces: routes `POST /api/auth/register` (201), `POST /api/auth/login` (200), `POST /api/auth/refresh` (200), `POST /api/auth/logout` (204), `GET /api/auth/me` (200); status codes and error codes per design section 3. On `auth.invalid_refresh_token` the controller deletes the cookie; on `auth.refresh_superseded` it does not.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Register_ThenMe_WithReturnedAccessToken_Returns200()            // 201; Set-Cookie has __Secure-refresh_token, httponly, secure, samesite=strict, path=/api/auth; body has no "refreshToken" property
[Fact] public async Task Login_MixedCaseEmail_Succeeds()                                 // registered "rahim@x.com", login " Rahim@X.COM "
[Fact] public async Task Login_WrongPassword_Returns401InvalidCredentials()
[Fact] public async Task Refresh_WithCookie_RotatesCookieAndOldCookieWithinGraceIsSuperseded()  // second refresh with old value -> 401 auth.refresh_superseded, response has no Set-Cookie deleting it
[Fact] public async Task Refresh_GarbageCookie_Returns401AndDeletesCookie()
[Fact] public async Task Refresh_ForeignOrigin_Returns403OriginNotAllowed()
[Fact] public async Task Refresh_NoOriginHeader_IsAllowed()
[Fact] public async Task Logout_ThenRefreshWithSameCookie_Returns401_OtherDeviceStillRefreshes()  // two logins = two devices
[Fact] public async Task Logout_WithoutCookie_Returns204()
[Fact] public async Task Login_RedisStopped_StillReturns200()                           // RedisFixture stops its container in this test's own fixture instance
```

- [ ] **Step 2: Run** integration and architecture tests. Expected: build fails.
- [ ] **Step 3: Implement**; every action has `[ProducesResponseType]` for each status; regenerate `docs/openapi.json`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add auth endpoints with refresh cookie`

**MILESTONE 3: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 4: Users and seeding

### Task 10: User list, assignable users, role change

Design: sections 3 (users endpoints), 5 (Role change), 6 (policies).

**Files:**
- Create: `src/Application/Features/Users/{UserDto,AssignableUserDto,UserSortFields}.cs`, `Queries/ListUsers/{ListUsersQuery,ListUsersQueryHandler,ListUsersQueryValidator}.cs`, `Queries/ListAssignableUsers/{ListAssignableUsersQuery,ListAssignableUsersQueryHandler,ListAssignableUsersQueryValidator}.cs`, `Commands/ChangeUserRole/{ChangeUserRoleCommand,ChangeUserRoleCommandHandler,ChangeUserRoleCommandValidator}.cs`, `src/Api/Auth/AuthorizationPolicies.cs` (`AdminOnly`, `ProjectManagerOrAdmin`), `src/Api/Controllers/Users/{UsersController,ListUsersRequest,ListAssignableUsersRequest,ChangeUserRoleRequest}.cs`
- Modify: `IUserReadRepository` / `UserReadRepository` (list methods), `AuthenticationExtensions.AddJwtAuthentication` (policies)
- Test: `tests/UnitTests/Application/Users/ChangeUserRoleCommandHandlerTests.cs`, `tests/IntegrationTests/Users/UsersEndpointsTests.cs`

**Interfaces:**
- Produces:
  - `record UserDto(Guid Id, string FullName, string Email, Role Role, DateTimeOffset CreatedAt)`; `record AssignableUserDto(Guid Id, string FullName, string Email)`
  - `UserSortFields`: `fullName`, `email`, `role`, `createdAt` (default `createdAt`)
  - `record ListUsersQuery(string? Search, Role? Role, string? SortBy, SortDirection SortDirection, int Page, int PageSize) : ListQuery(...), IQuery<PagedResult<UserDto>>`
  - `record ListAssignableUsersQuery(string? Search, int Page, int PageSize) : IQuery<PagedResult<AssignableUserDto>>` (sorted by full name; caller from `ISecurityContext`: Admin -> all users; ProjectManager -> all Team Members plus the caller)
  - `record ChangeUserRoleCommand(Guid UserId, Role Role) : ICommand`
  - `IUserReadRepository`: `Task<PagedResult<UserDto>> ListAsync(ListUsersQuery query, CancellationToken ct)`, `Task<PagedResult<AssignableUserDto>> ListAssignableAsync(ListAssignableUsersQuery query, Guid callerId, bool includeAllRoles, CancellationToken ct)`
  - Routes: `GET /api/users` (`AdminOnly`), `PUT /api/users/{id}/role` (`AdminOnly`, 204), `GET /api/users/assignable` (`ProjectManagerOrAdmin`)

- [ ] **Step 1: Write the failing tests**

```csharp
// Unit (order: Redis write, then revoke + one SaveChanges)
[Fact] public async Task ChangeRole_Valid_WritesRedisThenRevokesTokensAndSavesOnce()   // Received.InOrder: SetIfHigherAsync(id, 2) then SaveChangesAsync
[Fact] public async Task ChangeRole_RedisUnavailable_Returns503AndSavesNothing()        // Error == AuthErrors.Unavailable; SaveChangesAsync not received
[Fact] public async Task ChangeRole_SameRole_SucceedsWithoutRedisOrSave()
[Fact] public async Task ChangeRole_LastAdminDemoted_ReturnsLastAdmin()                 // CountByRoleAsync(Admin) == 1, target is Admin -> UserErrors.LastAdmin
[Fact] public async Task ChangeRole_UnknownUser_ReturnsNotFound()
// Integration (SQL Server + Redis)
[Fact] public async Task ChangeRole_OldAccessTokenOfTarget_Returns401OnNextRequest()
[Fact] public async Task ChangeRole_AdminDemotesSelfWithSecondAdmin_SucceedsAndOwnTokenIs401()
[Fact] public async Task ChangeRole_AsProjectManager_Returns403()
[Fact] public async Task Assignable_AsProjectManager_ReturnsTeamMembersAndSelfOnlyWithNameAndEmail()  // JSON object has exactly id, fullName, email
[Fact] public async Task ListUsers_SortByPassword_Returns400()
```

- [ ] **Step 2: Run** tests. Expected: build fails.
- [ ] **Step 3: Implement**; regenerate `docs/openapi.json`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: add user management and role change`

### Task 11: First-Admin seeding

Design: section 8.

**Files:**
- Create: `src/Infrastructure/Configuration/Seeding/{AdminSeedOptions,AdminSeedOptionsValidator,HostApplicationBuilderExtensions}.cs` (`ConfigureSeeding`), `src/Infrastructure/Seeding/AdminSeeder.cs` (scoped, `Task SeedAsync(CancellationToken ct)`), `src/Infrastructure/Seeding/AdminSeedHostedService.cs`
- Modify: `src/Infrastructure/Configuration/HostApplicationBuilderExtensions.cs` (call `ConfigureSeeding` after `ConfigureDatabase`, so the development migration runs first), `.env.example` (`ADMIN_SEED_EMAIL`, `ADMIN_SEED_PASSWORD`, `ADMIN_SEED_FULL_NAME` dev placeholders), `docker-compose.yml` (pass them as `AdminSeed__*`)
- Test: `tests/IntegrationTests/Infrastructure/AdminSeederTests.cs`, `tests/IntegrationTests/Api/StartupValidationTests.cs` (new cases)

**Interfaces:**
- Produces: `AdminSeedOptions` (`SectionName = "AdminSeed"`; `Email`, `Password`, `FullName`, all optional strings); validator: all empty -> valid (skip); some set -> invalid; password breaks `PasswordRules` -> invalid. Unique violation detection: `DbUpdateException` whose inner `SqlException.Number` is 2601 or 2627.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task Seed_NoAdminAndSettings_CreatesAdmin()
[Fact] public async Task Seed_AdminExists_DoesNothing()
[Fact] public async Task Seed_TwoSeedersInParallel_ExactlyOneAdmin()               // Task.WhenAll on two scopes; CountByRole(Admin) == 1, no exception
[Fact] public async Task Seed_EmailBelongsToTeamMember_ThrowsAndDoesNotPromote()   // role stays TeamMember; exception message names AdminSeed:Email
[Fact] public void Startup_PartialAdminSeedSettings_FailsValidation()              // only Email set -> OptionsValidationException on host start
[Fact] public void Startup_NoAdminSeedSettings_Starts()
```

- [ ] **Step 2: Run** tests. Expected: build fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: seed the first Admin from secret configuration`

**MILESTONE 4: run `bash scripts/verify.sh`, show the output, stop for the developer.**

## Milestone 5: Platform, Sample, documentation

### Task 12: Shared rate limiting through Redis

Design: section 10; ADR 0013 (rate limiting).

**Files:**
- Create: `src/Infrastructure/Redis/IFixedWindowCounter.cs` (`Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken ct)`; throws `SharedStateUnavailableException`), `src/Infrastructure/Redis/RedisFixedWindowCounter.cs` (Lua: `INCR`, then `PEXPIRE` when the result is 1), `src/Api/RateLimiting/SharedFixedWindowRateLimiter.cs` (`PartitionedRateLimiter<HttpContext>`), `src/Api/RateLimiting/AuthRateLimitAttribute.cs` (marker on login and register), `tests/IntegrationTests/Api/TestSupport/InMemoryFixedWindowCounter.cs`
- Modify: `src/Api/Extensions/RateLimitingExtensions.cs` (global limiter = the shared limiter; the current in-memory limiter becomes its fallback), `src/Api/Options/RateLimitingOptions.cs` (`AuthPermitLimit = 10`, `AuthWindowSeconds = 60`), `src/Api/appsettings.json`, `AuthController` (marker), `ApiFactory` (in-memory counter by default)
- Test: `tests/IntegrationTests/Api/RateLimitingTests.cs` (existing test keeps passing), `tests/IntegrationTests/Api/SharedRateLimitingTests.cs`

**Interfaces:**
- Produces: key `ratelimit:<policy>:<client-ip>:<window-start-unix-seconds>`, policy `global` or `auth`; a request on a marked endpoint must pass both the global and the auth limit; on `SharedStateUnavailableException` the in-memory limiter decides and a warning is logged (at most once per window).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task TwoHosts_SharingRedis_ShareOneLimit()          // two ApiFactory instances WithRedis, limit 3: requests 1-3 spread over both -> 200, 4th -> 429 rate_limited
[Fact] public async Task Login_ElevenTimesInAMinute_EleventhIs429()
[Fact] public async Task RedisDown_FallsBackToInstanceLimit()           // InMemoryFixedWindowCounter.SimulateOutage; limit 2 -> third request 429
[Fact] public async Task Health_IsNotRateLimited()
```

- [ ] **Step 2: Run** tests. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: share the per-IP rate limit across instances`

### Task 13: Forwarded headers and instance id in logs

Design: sections 11 and 12.

**Files:**
- Create: `src/Api/Options/TrustedProxyOptions.cs` (`SectionName = "ForwardedHeaders"`, `IReadOnlyList<string> KnownProxies` validated as IP addresses), `src/Api/Extensions/ForwardedHeadersExtensions.cs` (`AddTrustedForwardedHeaders`, `UseTrustedForwardedHeaders`)
- Modify: `src/Api/Program.cs` (`UseTrustedForwardedHeaders` first), `src/Api/Extensions/LoggingExtensions.cs` (`.Enrich.WithProperty("InstanceId", configuration["Instance:Id"] ?? Environment.MachineName)`), `src/Api/appsettings.json` (`"ForwardedHeaders": { "KnownProxies": [] }`)
- Test: `tests/IntegrationTests/Api/ForwardedHeadersTests.cs`, `tests/IntegrationTests/Api/LoggingConfigurationTests.cs` (new case)

- [ ] **Step 0: Verify** the .NET 10 `ForwardedHeadersOptions` member names for trusted proxies and networks and how to clear the defaults, from the official docs (https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer). Write down what was verified in the commit message.
- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task XForwardedFor_FromUntrustedAddress_IsIgnored()      // empty KnownProxies; rate limit partitions by the real remote address, not the header
[Fact] public async Task XForwardedFor_FromTrustedProxy_UsesClientAddress()  // test server remote address added to KnownProxies; two clients with different headers get separate limits
[Fact] public void Startup_InvalidProxyAddress_FailsValidation()              // KnownProxies: ["not-an-ip"]
[Fact] public async Task LogEvents_CarryInstanceId()                          // CapturingSink: Instance:Id = "test-1" -> property InstanceId == "test-1"
```

- [ ] **Step 2: Run** tests. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: trust forwarded headers only from configured proxies`

### Task 14: Sample behind `[Authorize]`, Phase 2 checklist

Design: section 3 (Sample), section 15.

**Files:**
- Create: `src/Api/OpenApi/AuthOperationTransformer.cs` (Bearer requirement on operations without `[AllowAnonymous]`; pattern from https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0, verify before writing), `tests/IntegrationTests/Api/TestSupport/AuthenticatedClient.cs` (`static Task<HttpClient> ForNewTeamMemberAsync(ApiFactory factory, CancellationToken ct)` and `ForSeedAdminAsync`: register or log in through the real endpoints, attach the bearer token)
- Modify: `src/Api/Controllers/Samples/SamplesController.cs` (every action `[Authorize]`, marker comments removed), `tests/ArchitectureTests/ControllerAuthorizationTests.cs` (Sample exemption removed; only the four auth actions allowed), `tests/IntegrationTests/Samples/*` (use `AuthenticatedClient`), `src/Api/Extensions/OpenApiExtensions.cs`, `docs/sample-flow.md` (Phase 2 checklist marked done, Swagger steps use login), `../.claude/rules/security.md`
- Test: `tests/IntegrationTests/Samples/SampleAuthorizationTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public async Task ListSamples_NoToken_Returns401ProblemDetails()
[Fact] public async Task ListSamples_WithToken_Returns200()
[Fact] public async Task ListUsers_AsTeamMember_Returns403ProblemDetails()   // errorCode present
[Fact] public void OpenApi_ProtectedOperation_HasBearerRequirement_AnonymousDoesNot()  // /api/samples GET has security; /api/auth/login has none
```

- [ ] **Step 2: Run** tests. Expected: FAIL.
- [ ] **Step 3: Implement**; regenerate `docs/openapi.json`.
- [ ] **Step 4: Run** all tests. Expected: PASS.
- [ ] **Step 5: Commit** `feat: require authentication on Sample endpoints`

### Task 15: Documentation and final verification

Design: section 16.

**Files:**
- Modify: `../docs/CONTEXT.md` (Redis exception to "no caching", 10-minute access token, forwarded headers done, Auth state "built"), `README.md` (Redis in setup; user-secrets for `Redis:ConnectionString` and `AdminSeed:*`; idempotent migration script `dotnet ef migrations script --idempotent`; seed recovery when the email is taken; seeded Admin password cannot be changed in v1), `docs/sample-flow.md` (secrets one-liners include the new keys), `docs/architecture.md` (auth flow and Redis in one short section)

- [ ] **Step 1:** Follow the README from a clean clone in Git Bash: `cp .env.example .env`, `docker compose up -d db redis`, set user-secrets, `dotnet run --project src/Api --launch-profile https`, then in Swagger: register -> login -> `GET /api/samples` with the token -> 200. Note every step that did not work and fix the README.
- [ ] **Step 2:** `bash scripts/verify.sh`. Expected: all steps pass.
- [ ] **Step 3: Commit** `docs: document authentication setup and Redis`

**MILESTONE 5: show the final verify output; do not push without the developer's go-ahead.**
