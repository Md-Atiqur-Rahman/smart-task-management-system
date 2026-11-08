# Patterns: use when / do NOT use when

## Options (+ ValidateOnStart)
- Use when: any setting is read (connection, JWT, paging, CORS, rate limits).
  Bind in a composition-root extension, validate with data annotations or
  `IValidateOptions<T>`, call `ValidateOnStart()`.
- Do NOT use when: the value is a true constant of the domain; never read
  `IConfiguration` in handlers or domain code instead.

## Strategy
- Use when: one operation has several interchangeable algorithms chosen at
  runtime (for example notification channels), each behind one interface.
- Do NOT use when: there is one implementation, or a simple `switch` on an enum
  in one place is clearer.

## Factory
- Use when: creating an object needs validation or invariants
  (`Sample.Create(...)`), or the concrete type is chosen at runtime.
- Do NOT use when: a constructor with no rules is enough (DTOs, records).

## State machine (Result-returning transitions)
- Use when: an aggregate has a status with allowed transitions. Each transition
  is a method returning `Result`; an invalid transition returns a `Conflict`
  error (`Sample.Complete()`).
- Do NOT use when: the status is only data with no rules, or for workflows that
  span aggregates (use separate use cases).

## Value Object
- Use when: a concept is defined by its values, is immutable and has rules
  (money, email, date range). Derive from `ValueObject`.
- Do NOT use when: the thing has identity or a lifecycle (that is an entity).

## Optimistic concurrency
- Use when: an aggregate can be changed by concurrent requests. Map
  `RowVersion` with `IsRowVersion()`; `IUnitOfWork` returns
  `concurrency.conflict` (409) for a stale write.
- Do NOT use when: data is append-only or never updated.

## Custom middleware
- Use when: something must run once per request for every endpoint (security
  context, security headers).
- Do NOT use when: it belongs to one endpoint or use case (use the handler or a
  filter) or the framework already offers it (CORS, rate limiting, auth).

## Write repository + read repository
- Use when: always for persistence. Write repository per aggregate root (Domain
  interface, tracked aggregates); read repository per feature (Application
  interface, DTOs, `AsNoTracking`, `Select` projections).
- Do NOT use when: never expose `DbContext` or `IQueryable` to Application;
  no generic repository, no Specification pattern.

## Result
- Use when: a failure is expected (validation, not found, conflict, forbidden).
- Do NOT use when: the failure is a bug or broken invariant (throw), or for
  infrastructure outages (let the exception reach the global handler).

## HostApplicationBuilderExtensions (Application / Infrastructure)
- Use when: registering services of Application or Infrastructure:
  `Configure<Concern><TBuilder>(this TBuilder) where TBuilder :
  IHostApplicationBuilder`, called from `ConfigureApplication` /
  `ConfigureInfrastructure`.
- Do NOT use when: in the Api project (architecture test fails).

## Add* / Use* extensions (Api)
- Use when: registering Api services (`Add<Concern>(IServiceCollection[,
  IConfiguration])`) or pipeline steps (`Use<Concern>(WebApplication)`).
- Do NOT use when: in Application or Infrastructure (architecture test fails),
  or inline in `Program.cs`.
