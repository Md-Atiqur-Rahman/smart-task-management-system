# ADR 0001: CQRS Lite with own handler interfaces

- Status: Accepted
- Date: 2026-11-08

## Context

Use cases must be small, testable units with one place for cross-cutting
behaviour (validation, logging). Reads and writes have different needs: writes
load aggregates and enforce invariants; reads project straight to DTOs. A
mediator library is not wanted: it adds a dependency and hides the call path
behind runtime dispatch.

## Decision

- Own interfaces in `src/Application/Abstractions/Messaging`: `ICommand`,
  `ICommand<TResponse>`, `IQuery<TResponse>` (marker interfaces used as generic
  constraints) and `ICommandHandler<TCommand>`,
  `ICommandHandler<TCommand, TResponse>`, `IQueryHandler<TQuery, TResponse>`.
  Every handler returns `Result` or `Result<T>`.
- Controllers inject the handler interface directly (`[FromServices]`); there is
  no dispatcher.
- The pipeline is hand-written decoration, registered by `HandlerRegistration`:
  Logging (outermost) -> Validation (FluentValidation) -> handler. An invalid
  request returns a `ValidationError` and never reaches the handler.
- One folder per use case: `Features/<Module>/Commands|Queries/<UseCase>/`
  with the command or query, handler and validator together.
- CQRS "lite": one database and one `DbContext`; the separation is in the code
  path (write repository + `IUnitOfWork` vs read repository + DTO), not in storage.

## Consequences

- The call path is visible in code: controller -> handler interface ->
  decorators -> handler. No runtime magic beyond DI.
- Adding a cross-cutting behaviour means one more decorator per handler kind
  (three classes) and one line in `HandlerRegistration`.
- Architecture tests enforce handler naming/location and that query handlers
  inject only read repositories.
- CA1040 (empty interfaces) is suppressed only on the three marker interfaces,
  with a justification.
