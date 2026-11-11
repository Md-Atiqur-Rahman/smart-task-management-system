# Architecture rules

Paths in this file are relative to the backend folder `<PROJECT_NAME>-Backend/`
(ADR 0019).

Decision records: `docs/adr/`. Overview: `docs/architecture.md`.

- Dependency rule: Domain -> nothing; Application -> Domain; Infrastructure ->
  Application + Domain; Api -> Application + Infrastructure (composition root
  only, no business logic). Architecture tests fail on any other reference.
- Interfaces inside, implementations in Infrastructure: write repositories and
  `IUnitOfWork` in Domain; read repositories, `ISecurityContext`,
  `IPasswordHasher` in Application.
- Application and Domain never reference EF Core, `Microsoft.Data.SqlClient` or
  any persistence package.
- Write path: controller -> command handler -> write repository + aggregate
  method -> `IUnitOfWork.SaveChangesAsync`. Read path: controller -> query
  handler -> `I<Name>ReadRepository` -> DTO. A query handler never loads an
  aggregate.
- DI style by project (ADR 0010):
  - Application/Infrastructure: `Configure<Concern><TBuilder>(this TBuilder)
    where TBuilder : IHostApplicationBuilder`, returning the builder, in
    `<Layer>/Configuration/<Concern>/HostApplicationBuilderExtensions.cs`; one
    entry point per layer (`ConfigureApplication`, `ConfigureInfrastructure`).
  - Api: `Add<Concern>(this IServiceCollection[, IConfiguration])` and
    `Use<Concern>(this WebApplication)` in `Api/Extensions/<Concern>Extensions.cs`.
  - `Program.cs` only calls those; no inline registration.
- Api surface: only controllers and their request/response types are public
  (everything else `internal`).
- One `/health` endpoint (database check), anonymous; no `/alive`.
- Tests that enforce this: `tests/ArchitectureTests` (layers, persistence
  references, controllers, authorization attributes, handlers, entities, DI
  style, `Program.cs`, API surface, NullGuard gap, WeavingProbe references).
