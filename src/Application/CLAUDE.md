# Application layer rules

- References Domain only. Allowed packages: hosting/options abstractions and
  FluentValidation. NEVER EF Core or any persistence package.
- Use cases in `Features/<Module>/Commands|Queries/<UseCase>/` (command or query,
  handler, validator together). Handlers implement `ICommandHandler<...>` /
  `IQueryHandler<...>` and return `Result` / `Result<T>`.
- Command handlers: write repository (Domain interface) + aggregate method +
  `IUnitOfWork.SaveChangesAsync`. Query handlers: ONLY `I<Name>ReadRepository`
  (declared here, returns DTOs); never a write repository, `IUnitOfWork` or a
  `DbContext`, never an aggregate.
- Every command and list query has a validator; list queries derive from
  `ListQuery` and `ListQueryValidator<T>` (sort whitelist, page-size cap).
- Time via `TimeProvider`; identity via `ISecurityContext` (never the setter,
  never `HttpContext`).
- DI style: `Configure<Concern><TBuilder>(this TBuilder) where TBuilder :
  IHostApplicationBuilder` in `Configuration/<Concern>/HostApplicationBuilderExtensions.cs`,
  called only from `ConfigureApplication()`. No `Add*`/`Use*` extension methods.
- Handlers and validators are discovered automatically (`HandlerRegistration`,
  `AddValidatorsFromAssembly`); do not register them by hand.
