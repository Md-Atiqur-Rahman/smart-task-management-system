# Domain layer rules

- References NOTHING (no other project, no EF Core, no ASP.NET Core). Pure C#.
- Contains: aggregates and entities (`Primitives/`), value objects, `Result` /
  `Error` / `ErrorType` / `ValidationError` (`Results/`), `AppException` and its
  sealed subclasses (`Exceptions/`), `IUnitOfWork`, write repository interfaces
  (`<Module>/I<Aggregate>Repository.cs`), `<Aggregate>Errors`.
- Aggregates: `Create(...)` factory with `Guard` clauses, private setters only,
  state changes as methods; return `Result` when user input can make them fail;
  reference other aggregates only by Id.
- No time source here: methods that need time take a `DateTimeOffset` parameter;
  audit fields are set by Infrastructure.
- No HTTP knowledge: `ErrorType` describes the failure, the Api maps it.
- No DI registration: this layer has no `Configure*` or `Add*` methods.
- NullGuard is active: write `?` honestly; no `null!`.
