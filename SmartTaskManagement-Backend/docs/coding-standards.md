# Coding standards

Every rule below is enforced by the build, an architecture test or
`scripts/verify` unless marked "guideline".

## Build and analyzers

- `net10.0`, `Nullable` enabled, `TreatWarningsAsErrors` in every configuration,
  `AnalysisMode` `All`, `EnforceCodeStyleInBuild`. A build must show 0 warnings.
- The global `NoWarn` (`Directory.Build.props`) holds only CA2007 (ASP.NET Core
  has no SynchronizationContext), CS1591 (XML docs are not required on every
  member) and CA1062 (NullGuard and nullable annotations cover argument checks).
- Every other decision is scoped and carries its reason (table below).
- A new analyzer finding is fixed in code. Suppressing it needs a reason and the
  team's approval.

### Analyzer decisions

Scoped suppressions:

| Rule | Scope | Decision | Reason |
| --- | --- | --- | --- |
| CA1707 | Test projects only (`Directory.Build.targets`, `IsTestProject`) | Disabled | Test names follow `Method_Condition_ExpectedResult` |
| CA1515 | Test projects only (`Directory.Build.targets`, `IsTestProject`) | Disabled | xUnit requires public test classes (xUnit1000) and public collection definitions (xUnit1027); fixtures injected into public test constructors must then be public too (proven by a real build) |
| CA1515 | `src/Api/Controllers/**.cs` only (`.editorconfig`) | Disabled | MVC discovers only public controllers (an internal controller returned 404 on every endpoint in a real run), and request/response types in public action signatures must be public. Compensating control: architecture test `PublicTypesInApi_OnlyInControllersOrContracts` keeps every other Api type internal |
| CA1032 | `src/Domain/Exceptions/*.cs` only (`.editorconfig`) | Disabled | The standard parameterless and message-only constructors would create exceptions without `ErrorCode` and `Title`, which the error model forbids |
| CA1716 | The `Error` type only | Targeted `[SuppressMessage]` | `Error` is a keyword only in Visual Basic; this C#-only code base has no VB consumers, and `Error` is the conventional name in the Result pattern |
| CA1819 | The `RowVersion` property only | Targeted `[SuppressMessage]` | EF Core maps the SQL Server `rowversion` concurrency token to `byte[]` |
| CA1040 | `ICommand`, `ICommand<TResponse>`, `IQuery<TResponse>` only | Targeted `[SuppressMessage]` | Marker interfaces used as generic constraints: they identify commands and queries at compile time, so a query can never reach a command handler and the reverse. The CA1040 documentation names "identifies a set of types at compile time" as a valid reason to suppress |

Not suppressed:

| Rule | Scope | Note |
| --- | --- | --- |
| CA1515 | `tests/WeavingProbe` | Active. It is not a test project (no test-only `NoWarn`); by its documented scope the rule analyzes executable assemblies, and WeavingProbe is a library, so it reports nothing there |

Findings fixed in code (keep them fixed; do not suppress):

| Rule | Where | Fix | Reason |
| --- | --- | --- | --- |
| CA1716 | `ISecurityContextSetter.Set` | Renamed to `SetPrincipal` | `Set` is a Visual Basic keyword; a real naming issue on an interface member |
| CA1008 | `ErrorType` | `Validation = 0` | An enum needs a zero value; `ErrorType` is never persisted, so renumbering is safe |
| CA1515 | UnitTests helper types | Made `internal`, plus `InternalsVisibleTo("DynamicProxyGenAssembly2")` | NSubstitute (Castle DynamicProxy) must see internal types used as generic arguments |
| CA1515 | Api helper types (extensions, options, errors, middleware) | Made `internal`, plus `InternalsVisibleTo` for IntegrationTests and ArchitectureTests | An executable exposes no public API except what MVC needs |
| CA1812 | `SampleConfiguration`, `DevelopmentMigrationService` | Made `public sealed` | EF Core and DI create them through reflection, so the analyzer sees no instantiation |
| CA1711 | `SqlServerCollection` | Renamed `SqlServerCollectionDefinition` | Type names must not end in `Collection` unless they are collections |
| CA1305 | Serilog console sink | `formatProvider: CultureInfo.InvariantCulture` | Culture-independent log formatting |
| CA1822 | Test-only controller action | Uses its `ControllerContext` | MVC actions must be instance methods |
| CA1852 | `ApiFactory` | `sealed` | No subtypes |
| CA1861 | Constant array argument in a test | `static readonly` field | Avoids allocating the array on every call |
| CA2234 | `SamplesController.Create` | `Created(Uri, ...)` | Pass `Uri`, not `string` |
| CA1873 | `GlobalExceptionHandler` log call | Logger parameter typed `PathString` | Avoids a conversion when the log level is disabled |
| NU1902, NU1903 | Transitive `System.Security.Cryptography.Pkcs` 9.0.11 | Transitive pin to 10.0.14 (ADR 0012) | NuGetAudit advisories; patched version |

## Formatting

- `dotnet format` is the only formatter; `.editorconfig` is the single source
  (LF line endings, UTF-8, final newline; `.gitattributes` `* text=auto eol=lf`).
  Verify runs `dotnet format --verify-no-changes`.
- `using` groups are separated by a blank line (template setting).
- Naming rules are at `suggestion`; `dotnet_style_readonly_field` and
  `csharp_prefer_static_local_function` are `warning`.
- 80 characters per line is a guideline only (`dotnet format` does not wrap).

## Naming

- Projects: `src/{Domain,Application,Infrastructure,Api}`,
  `tests/{UnitTests,IntegrationTests,ArchitectureTests,WeavingProbe}`;
  assembly and root namespace `<Company>.<Project>.<Folder>`, set once in
  `Directory.Build.props`.
- Use cases: `<Verb><Noun>Command|Query`, `...Handler`, `...Validator` in
  `Features/<Module>/Commands|Queries/<UseCase>/`.
- Error codes: `<module>.<snake_case_reason>`, stable once published.
- Tests: `Method_Condition_ExpectedResult`.

## Domain

- Entities and aggregates: private setters, `Create(...)` factories with guard
  clauses, state changes as methods (returning `Result` when user input can make
  them fail). No public setters (architecture test). Aggregates reference each
  other only by Id.
- Value objects derive from `ValueObject` and are immutable.

## Nullability and time

- Write `?` honestly; NullGuard enforces non-nullable parameters and return
  values at runtime (`Mode=NullableReferenceTypes`). Do not use `null!` in src.
- Non-nullable async results use `Task<T>`, never `ValueTask<T>` (NullGuard does
  not check `ValueTask<T>` results; architecture test).
- Time comes from `TimeProvider`; `DateTime.UtcNow` / `DateTime.Now` are not used.

## Errors and responses

- Expected failures: `Result` with an `Error` (see ADR 0004). Unexpected:
  exceptions; never swallow `catch (Exception)`.
- The Api maps errors only through `ErrorResults.ToProblem`.
- Every action declares `[Authorize]` or `[AllowAnonymous]` and a
  `[ProducesResponseType]` for every status it can return.

## Configuration and secrets

- Every setting is an Options class bound with `ValidateOnStart()`. Only the
  composition-root extension methods and Serilog read `IConfiguration`.
- Secrets live only in user-secrets (local) or environment variables; never in
  tracked files. `.env.example` holds safe dev-only placeholders.

## Logging

- Serilog through `AddSerilog(..., preserveStaticLogger: true)`; no static
  `Log.Logger` use.
- Log messages use `[LoggerMessage]` source-generated methods. Never log request
  bodies or secrets.

## Tests

- xunit.v3 on Microsoft.Testing.Platform, NSubstitute, Shouldly, NetArchTest,
  Testcontainers (SQL Server). No FluentAssertions or Moq.
- Integration tests use the real host (`ApiFactory`) and a real SQL Server
  (`SqlServerFixture`).

## Commits

- Prefixes: `feat:`, `fix:`, `docs:`. One logical change per commit.
- When controllers or DTOs change, regenerate and commit `docs/openapi.json`
  (`bash scripts/generate-openapi.sh`); verify fails otherwise.
