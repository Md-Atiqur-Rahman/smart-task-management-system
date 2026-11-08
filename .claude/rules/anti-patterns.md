# Anti-patterns (forbidden)

Each item: what is forbidden, and what to do instead.

- **Service Locator** (resolving services from `IServiceProvider` in business
  code). Inject dependencies through constructors. Composition-root code
  (`HandlerRegistration`, middleware wiring) is the only exception.
- **Static or global state.** No mutable statics. Serilog's static `Log.Logger`
  is not used: `AddSerilog(..., preserveStaticLogger: true)` keeps one logger
  per host (several test hosts run in one process).
- **God service.** One handler per use case; no "SampleService" with every
  operation.
- **Anemic entity.** Behaviour and invariants live on the aggregate, not in
  handlers.
- **Returning entities from the API.** Return DTOs (`SampleDto`) or
  `PagedResult<T>`; entities never cross the Api boundary.
- **Swallowing `catch (Exception)`.** Let unexpected exceptions reach the global
  exception handler (generic 500, details logged). Catch only specific,
  expected exceptions (for example `DbUpdateConcurrencyException` -> `Conflict`).
- **Magic strings and numbers.** Use named constants (`Sample.NameMaxLength`,
  `SampleSortFields`, `SecurityClaimTypes`, Options).
- **Public setters on entities** (`init` included). Private setters plus methods.
- **`DateTime.UtcNow` / `DateTime.Now`.** Inject `TimeProvider`.
- **`IConfiguration` in business code.** Bind to validated Options in a
  composition-root extension; only those extensions and Serilog read
  configuration.
- **Patterns used for show.** Add a pattern only for a current need (see
  patterns.md "do NOT use when").
- **Disabled TLS certificate validation.** The certificate-trust connection
  keyword is forbidden everywhere; `Encrypt=false` only in Development/Testing
  (ADR 0011).
- **Hardcoded secrets, database names or image names.** Secrets in user-secrets
  or environment variables; names from `.env.example` / `Directory.Build.props`.
- **EF Core in Application or Domain.** Persistence stays in Infrastructure.
- **User strings passed to `OrderBy`.** Sort only by whitelisted keys mapped to
  expressions (`ApplySort`).
- **Inline service registration in `Program.cs`.** Use `Configure*` / `Add*`.
- **Mixing `Configure*` and `Add*`/`Use*` across layers.** The style is decided
  by the project (ADR 0010).
- **`null!` in src** to silence the compiler; it becomes a runtime exception
  under NullGuard. **`ValueTask<T>` for non-nullable reference results**;
  NullGuard does not check them; use `Task<T>`.
- **Class-level `[Produces("application/json")]` on controllers.** It overwrites
  `application/problem+json` on error responses.

## Public types in Api outside controllers

- Api is an executable: every type is `internal` except controllers and their
  request/response types under `src/Api/Controllers/` (and `src/Api/Contracts/`
  if that folder is ever added), plus the generated `Program`.
- CA1515 is disabled ONLY for `src/Api/Controllers/**.cs` (path-scoped
  `.editorconfig` section with the reason): MVC discovers public controllers only
  (an internal `SamplesController` returned 404 on every endpoint in a real run),
  and types in public action signatures must then be public.
- Compensating control: architecture test
  `ApiSurfaceTests.PublicTypesInApi_OnlyInControllersOrContracts` fails on any
  other public Api type. Do not widen the `.editorconfig` section; do not add
  CA1515 to any `NoWarn`.
