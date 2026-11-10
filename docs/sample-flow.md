# Sample flow

The `Sample` feature is a working demo and the copy-source for every new feature.
Every Sample file starts with `// SAMPLE: copy-source, delete or rename`.

## Endpoints

| Endpoint | Shows | Success | Failure |
| --- | --- | --- | --- |
| `POST /api/samples` | Command, validator, aggregate `Create`, UnitOfWork | 201 + `Location` + `{ "id" }` | 400 with `errors` |
| `GET /api/samples` | Query, read repository, search, sort whitelist, paging | 200 `PagedResult<SampleDto>` | 400 for bad sort, page or page size |
| `GET /api/samples/{id}` | Read repository, DTO projection | 200 `SampleDto` | 404 |
| `POST /api/samples/{id}/complete` | Domain state change returning `Result` | 204 | 404; 409 if already completed or changed concurrently |
| `DELETE /api/samples/{id}` | Soft delete | 204 | 404; 409 if changed concurrently |

All endpoints are `[Authorize]` (a valid access token is required; 401 without one).
Every error is ProblemDetails (`application/problem+json`) with an `errorCode`;
400 responses also carry an `errors` map (field -> messages).

## (a) Request path of a command

`POST /api/samples/{id}/complete`:

1. `SamplesController.Complete` (Api) binds the route id and calls the handler.
2. `CompleteSampleCommand` (Application) is passed to `ICommandHandler<CompleteSampleCommand>`.
3. Pipeline (Application, registered by `HandlerRegistration`):
   `LoggingDecorator` -> `ValidationDecorator` (FluentValidation; invalid -> 400, handler not called).
4. `CompleteSampleCommandHandler` (Application) loads the aggregate through
   `ISampleRepository` (Domain interface), calls `Sample.Complete()` (returns `Result`),
   then `IUnitOfWork.SaveChangesAsync`.
5. `SampleRepository` and `UnitOfWork` (Infrastructure) use `AppDbContext`; the
   `AuditInterceptor` stamps `UpdatedAt` from `TimeProvider`; a stale `RowVersion`
   becomes a `Conflict` result.
6. The controller maps the `Result`: success -> 204, failure -> `ErrorResults.ToProblem` (the one mapping place).

## (b) Request path of a query

`GET /api/samples?search=...&sortBy=name&page=1&pageSize=20`:

1. `SamplesController.List` (Api) binds `ListSamplesRequest` and builds `ListSamplesQuery`
   (page size defaults to `Paging:DefaultPageSize`).
2. Pipeline: Logging -> Validation (`ListSamplesQueryValidator`: page >= 1,
   1 <= pageSize <= `Paging:MaxPageSize`, defined direction, `SortBy` only from
   `SampleSortFields`).
3. `ListSamplesQueryHandler` (Application) calls `ISampleReadRepository` (Application interface).
4. `SampleReadRepository` (Infrastructure): `ApplySearch` (parameterized; `%`, `_`, `[`
   match literally) -> `ApplySort` (whitelist map, stable tie-breaker) ->
   `ToPagedResultAsync` (AsNoTracking, hand-written `Select` to `SampleDto`).
5. The controller returns 200 with the `PagedResult<SampleDto>` (no wrapper).

## (c) Which file lives where

| Step | Layer | File |
| --- | --- | --- |
| Aggregate, errors, write repository interface | Domain | `src/Domain/Samples/{Sample,SampleStatus,SampleErrors,ISampleRepository}.cs` |
| Commands (command, handler, validator) | Application | `src/Application/Features/Samples/Commands/{CreateSample,CompleteSample,DeleteSample}/` |
| Queries (query, handler, validator, sort whitelist) | Application | `src/Application/Features/Samples/Queries/{GetSampleById,ListSamples}/` |
| DTO and read repository interface | Application | `src/Application/Features/Samples/{SampleDto,ISampleReadRepository}.cs` |
| EF mapping | Infrastructure | `src/Infrastructure/Persistence/Configurations/SampleConfiguration.cs` |
| Write repository | Infrastructure | `src/Infrastructure/Persistence/Repositories/SampleRepository.cs` |
| Read repository | Infrastructure | `src/Infrastructure/Persistence/ReadRepositories/SampleReadRepository.cs` |
| DI registration | Infrastructure | `src/Infrastructure/Configuration/Database/HostApplicationBuilderExtensions.cs` |
| Migration | Infrastructure | `src/Infrastructure/Persistence/Migrations/*_InitialCreate*.cs` |
| Controller, request/response | Api | `src/Api/Controllers/Samples/` |
| Unit tests | UnitTests | `tests/UnitTests/Domain/SampleTests.cs`, `tests/UnitTests/Application/Samples/` |
| Integration and smoke tests | IntegrationTests | `tests/IntegrationTests/Samples/` |

## (d) Add a new feature by copying Sample

1. Domain: copy `src/Domain/Samples/` to `src/Domain/<Module>/`, rename `Sample` to the
   aggregate; keep `Create(...)`, guard clauses, private setters, `Result`-returning
   state changes, `<Module>Errors` with stable codes.
2. Application: copy `Features/Samples/` to `Features/<Module>/`; one folder per use case
   (command or query, handler, validator together). Define the sort whitelist.
3. Infrastructure: copy the configuration, write repository and read repository;
   register both repositories in `ConfigureDatabase`.
4. Migration: `dotnet tool restore`, then
   `Database__Name=design-time Database__Password=design-time dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations`.
5. Api: copy `Controllers/Samples/`; every action needs `[Authorize]` (new features are
   NOT anonymous; `[AllowAnonymous]` outside the four `AuthController` actions fails an architecture test) and
   `[ProducesResponseType]` for every status.
6. Tests: copy the unit, integration and smoke tests and adapt.
7. Regenerate and commit `docs/openapi.json`: `bash scripts/generate-openapi.sh`.
8. Run `scripts/verify`.

## (e) Run and test from Swagger

1. `cp .env.example .env` (git-ignored; used by Docker Compose).
2. `docker compose up -d db redis` (SQL Server and Redis on `127.0.0.1` only).
3. Copy the secrets into user-secrets once (the API does not read `.env`).
   Git Bash, from the repo root (`.env` is read line by line, not sourced,
   because `ADMIN_SEED_FULL_NAME` contains a space):

   ```bash
   g() { grep "^$1=" .env | cut -d= -f2-; }; for kv in "Database:Name=$(g DB_NAME)" "Database:Password=$(g DB_PASSWORD)" "Jwt:SigningKey=$(g JWT_SIGNING_KEY)" "Redis:ConnectionString=127.0.0.1:$(g REDIS_PORT),password=$(g REDIS_PASSWORD)" "AdminSeed:Email=$(g ADMIN_SEED_EMAIL)" "AdminSeed:Password=$(g ADMIN_SEED_PASSWORD)" "AdminSeed:FullName=$(g ADMIN_SEED_FULL_NAME)"; do dotnet user-secrets set "${kv%%=*}" "${kv#*=}" --project src/Api; done
   ```

   PowerShell, from the repo root:

   ```powershell
   $e=@{}; Get-Content .env | ? { $_ -match '^\w+=' } | % { $k,$v = $_ -split '=',2; $e[$k]=$v }; dotnet user-secrets set Database:Name $e.DB_NAME --project src/Api; dotnet user-secrets set Database:Password $e.DB_PASSWORD --project src/Api; dotnet user-secrets set Jwt:SigningKey $e.JWT_SIGNING_KEY --project src/Api; dotnet user-secrets set Redis:ConnectionString "127.0.0.1:$($e.REDIS_PORT),password=$($e.REDIS_PASSWORD)" --project src/Api; dotnet user-secrets set AdminSeed:Email $e.ADMIN_SEED_EMAIL --project src/Api; dotnet user-secrets set AdminSeed:Password $e.ADMIN_SEED_PASSWORD --project src/Api; dotnet user-secrets set AdminSeed:FullName $e.ADMIN_SEED_FULL_NAME --project src/Api
   ```

   Keys: `Database:Name`, `Database:Password`, `Jwt:SigningKey`,
   `Redis:ConnectionString`, `AdminSeed:Email`, `AdminSeed:Password`,
   `AdminSeed:FullName`. As environment variables: `Database__Name`,
   `Database__Password`, `Jwt__SigningKey`, `Redis__ConnectionString`,
   `AdminSeed__Email`, `AdminSeed__Password`, `AdminSeed__FullName` (set all three
   `AdminSeed` values or none). If you change `DB_PORT`, also set `Database:Port`.
4. `dotnet run --project src/Api --launch-profile https` (`http` if the development
   certificate is not trusted; Development: the initial migration applies
   automatically, then the first Admin is seeded).
5. Open `https://localhost:7254/swagger` (`http` profile: `http://localhost:5199/swagger`;
   Development only). Sign in first:
   `POST /api/auth/register` (or `POST /api/auth/login`), copy the `accessToken` from
   the response, click "Authorize" and paste it. Then:
   1. `POST /api/samples` with `{ "name": "first" }` -> 201, copy the `id`.
   2. `POST /api/samples` with `{ "name": "" }` -> 400 with `errors.Name`.
   3. `GET /api/samples?page=1&pageSize=5&sortBy=name` -> 200.
   4. `GET /api/samples?sortBy=password` -> 400.
   5. `GET /api/samples/{id}` -> 200; with a random GUID -> 404.
   6. `POST /api/samples/{id}/complete` -> 204; again -> 409.
   7. `DELETE /api/samples/{id}` -> 204; again -> 404.
   Without a token every Sample call returns 401 (ProblemDetails with `errorCode`).

## Phase 2 checklist (authentication)

Done. Phase 2 added authentication (see the Roadmap in `README.md`). Feature
modules are added by copying Sample.

1. [x] Every Sample action is `[Authorize]`; the `// SAMPLE ONLY: replace with
   [Authorize] in Phase 2` markers are gone.
2. [x] Architecture test 5 has no Sample exemption: `[AllowAnonymous]` is allowed
   only on the four `AuthController` actions (Register, Login, Refresh, Logout;
   ADR 0014).
3. [x] The Bearer security requirement is added per protected operation by
   `AuthOperationTransformer` (from
   https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0)
   next to `BearerSecuritySchemeTransformer` (which declares the scheme).
   Operations with `[AllowAnonymous]` get no requirement.
4. [x] Tests: no token -> 401, valid token -> success, wrong role -> 403
   (ProblemDetails with `errorCode`) in
   `tests/IntegrationTests/Samples/SampleAuthorizationTests.cs`; the Sample tests
   use `AuthenticatedClient`.
5. [x] `docs/openapi.json` regenerated and committed
   (`bash scripts/generate-openapi.sh`).
6. [x] The Swagger steps above register or log in and use the "Authorize" button.

## Remove Sample

1. Delete the Sample files (search for `// SAMPLE: copy-source, delete or rename`):
   `src/Domain/Samples/`, `src/Application/Features/Samples/`,
   `src/Infrastructure/Persistence/Configurations/SampleConfiguration.cs`,
   `src/Infrastructure/Persistence/Repositories/SampleRepository.cs`,
   `src/Infrastructure/Persistence/ReadRepositories/SampleReadRepository.cs`,
   `src/Api/Controllers/Samples/`, `tests/UnitTests/Domain/SampleTests.cs`,
   `tests/UnitTests/Application/Samples/`, `tests/IntegrationTests/Samples/`,
   and the Sample-based tests in `tests/IntegrationTests/Infrastructure/PersistenceTests.cs`.
2. Remove the two Sample repository registrations in `ConfigureDatabase`.
3. Delete `src/Infrastructure/Persistence/Migrations/` and recreate the initial
   migration for the remaining model (command in step 4 above).
4. Nothing to change in architecture test 5: the Sample `[AllowAnonymous]`
   exemption is already gone; only the four `AuthController` actions are anonymous.
5. Regenerate and commit `docs/openapi.json` (`bash scripts/generate-openapi.sh`).
6. Run `scripts/verify`.
