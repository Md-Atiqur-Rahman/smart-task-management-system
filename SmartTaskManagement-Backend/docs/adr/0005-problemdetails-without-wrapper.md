# ADR 0005: Raw DTOs on success, ProblemDetails on errors, no envelope

- Status: Accepted
- Date: 2026-11-08

## Context

Many APIs wrap every response in an envelope (`{ success, data, errors }`).
HTTP already has a status code, and RFC 9457 ("Problem Details for HTTP APIs")
defines a standard error body with the media type `application/problem+json`.

## Decision

- Success: the raw DTO, or `PagedResult<T>` (`items`, `page`, `pageSize`,
  `totalCount`) for lists, directly in the body. `201 Created` carries a
  `Location` header.
- Errors: always ProblemDetails (`application/problem+json`) with `status`,
  `title`, `detail`, `instance` and an `errorCode` extension (stable, machine
  readable). Validation failures (`400`) add an `errors` map
  (field -> messages), both for FluentValidation failures and for MVC's
  automatic 400 (malformed JSON).
- Framework-generated problems (unknown route 404, 405, 415, 429) get a default
  `errorCode` (`http.<status>`, `rate_limited`) and a `traceId`.
- Unhandled exceptions: a global `IExceptionHandler` returns a generic 500 with
  `errorCode: internal_error`. Details (message, stack trace) go only to the log
  (proven by a test with a captured log sink).

## Trade-off

- Pro: standard, self-describing errors; clients branch on status and
  `errorCode`; no envelope parsing on success; tools and proxies understand the
  media type.
- Con: there is no uniform place for response metadata on success (for example
  paging links). Paging data lives inside `PagedResult<T>`; other metadata would
  go in headers.

## Consequences

- Every action declares `[ProducesResponseType]` for each status it can return,
  with `application/problem+json` on error statuses. A class-level
  `[Produces("application/json")]` must not be used: it overwrote the problem
  media type (found by a test).
