# DDD rules

Paths in this file are relative to the backend folder `<PROJECT_NAME>-Backend/`
(ADR 0019).

- Building blocks in `src/Domain/Primitives`: `Entity<TId>` (equality by Id),
  `AggregateRoot<TId>`, `AuditableAggregateRoot<TId>` (CreatedAt, UpdatedAt,
  soft delete, `RowVersion`), `ValueObject` (equality by components), `Guard`.
- Aggregates:
  - created only through a static `Create(...)` factory with guard clauses;
  - private setters only (architecture test: no public setter, `init` included);
  - state changes are methods; a change that user input can make fail returns
    `Result` (example: `Sample.Complete()` -> `SampleErrors.AlreadyCompleted`);
  - reference other aggregates only by Id;
  - one write repository per aggregate root, declared in Domain.
- No anemic entities: behaviour lives on the aggregate, not in handlers.
- Errors of an aggregate live in `<Aggregate>Errors` with stable codes
  (`<module>.<reason>`).
- Guard clauses (programmer errors) throw `ArgumentException`; broken
  invariants throw a sealed `AppException` subclass. Never `throw new Exception`.
- Audit fields are set by Infrastructure from `TimeProvider`; soft delete takes
  the time as a parameter (`Delete(DateTimeOffset)`).
- EF Core materializes aggregates through a private constructor whose parameter
  names match the properties.
