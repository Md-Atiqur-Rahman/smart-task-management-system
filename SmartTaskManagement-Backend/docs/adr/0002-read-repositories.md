# ADR 0002: Read repositories instead of IQueryable in Application

- Status: Accepted
- Date: 2026-11-08

## Context

Query handlers need filtered, sorted, paged data. Two common options:

1. Expose `IQueryable<T>` (or the `DbContext`) to Application and compose
   queries in handlers.
2. Declare `I<Name>ReadRepository` interfaces in Application that return DTOs,
   and implement them in Infrastructure.

Application and Domain must never reference EF Core or any persistence package.

## Decision

Option 2.

- `I<Name>ReadRepository` lives next to the feature in Application and returns
  DTOs or `PagedResult<TDto>`, never entities.
- Implementations in `src/Infrastructure/Persistence/ReadRepositories` use
  `AsNoTracking` and hand-written `Select` projections straight to the DTO.
- Reusable `ApplySearch` / `ApplySort` / `ToPagedResultAsync` extensions on
  `IQueryable` (Infrastructure) are used by every read repository:
  - the search term is sent to SQL as a parameter and LIKE wildcards in it
    (`%`, `_`, `[`) match literally (proven against real SQL Server);
  - sorting accepts only whitelisted keys mapped to expressions; a user string
    never reaches `OrderBy`;
  - the page size is capped by `Paging:MaxPageSize` (validated Options).
- The list query object (`ListQuery`: Search, SortBy, SortDirection, Page,
  PageSize) and its validator base (`ListQueryValidator<T>`) live in Application.
- No Specification pattern.

## Trade-off

- Pro: Application stays free of EF Core (architecture test); query shape and
  performance are owned by Infrastructure; handlers are trivially unit-testable
  with a substituted read repository.
- Con: each new query shape needs a repository method; there is no ad-hoc
  composition in handlers. Accepted: queries are explicit and reviewable.
