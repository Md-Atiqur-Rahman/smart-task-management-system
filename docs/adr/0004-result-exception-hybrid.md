# ADR 0004: Result for expected failures, exceptions for unexpected ones

- Status: Accepted
- Date: 2026-11-08

## Context

Some failures are part of normal operation (not found, already completed,
validation, a concurrent change). Others are bugs or broken invariants. Using
exceptions for the first kind hides control flow and makes every caller guess;
using results for the second kind spreads defensive checks everywhere.

## Decision

- Expected failures return `Result` / `Result<T>` carrying an `Error`
  (`Code`, `Description`, `ErrorType`). `ErrorType` is `Validation`, `NotFound`,
  `Conflict`, `Forbidden`, `Unauthorized`. `ValidationError` adds a field ->
  messages map. Domain methods that can fail from user input return `Result`
  (for example `Sample.Complete()`).
- Error codes are stable and part of the API contract (`sample.not_found`,
  `sample.already_completed`, `concurrency.conflict`, `validation.failed`).
- Unexpected failures and broken invariants throw. Guard clauses (programmer
  errors) always throw `ArgumentException`. Domain invariants throw a sealed
  subclass of the abstract `AppException` (`ErrorCode`, `Title`).
  `throw new Exception(...)` is not used.
- A stale `RowVersion` is expected under concurrency: `UnitOfWork` turns
  `DbUpdateConcurrencyException` into a `Conflict` result.
- Domain and Application contain no HTTP knowledge. The Api maps `ErrorType` to
  a status in one place (`ErrorResults`): 400, 404, 409, 403, 401. Any exception
  that escapes becomes a generic 500 (see ADR 0005).

## Consequences

- Handlers read top to bottom; callers must handle the `Result`.
- `Result.Error` on success and `Result<T>.Value` on failure throw
  `InvalidOperationException` (programming errors).
- CA1032 (standard exception constructors) is disabled only for
  `src/Domain/Exceptions`, because constructors without `ErrorCode` and `Title`
  would break this model.
