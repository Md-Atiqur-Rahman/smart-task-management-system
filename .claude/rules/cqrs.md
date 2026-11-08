# CQRS Lite rules

- Own interfaces only (no MediatR): `ICommand`, `ICommand<TResponse>`,
  `IQuery<TResponse>`, `ICommandHandler<TCommand>`,
  `ICommandHandler<TCommand, TResponse>`, `IQueryHandler<TQuery, TResponse>`
  in `src/Application/Abstractions/Messaging`.
- Handlers return `Result` / `Result<T>`; expected failures never throw.
- Pipeline, registered by `HandlerRegistration` (hand-written, no Scrutor):
  Logging (outermost) -> Validation -> handler. An invalid request never reaches
  the handler.
- Use case layout: `Features/<Module>/Commands|Queries/<UseCase>/` with the
  command or query, handler, validator and DTO together.
- Query handlers inject only `I<Name>ReadRepository`; never a write repository,
  `IUnitOfWork` or `DbContext`, and never load an aggregate.

## CA1040 (Avoid empty interfaces)

`ICommand`, `ICommand<TResponse>` and `IQuery<TResponse>` are empty marker
interfaces on purpose: they are generic constraints that identify commands and
queries at compile time, so a query can never reach a command handler and a
command can never reach a query handler. Each carries a targeted
`[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = ...)]`
with the full reason. The rule stays active everywhere else; do not add CA1040 to
any `NoWarn` and do not create other empty interfaces.
