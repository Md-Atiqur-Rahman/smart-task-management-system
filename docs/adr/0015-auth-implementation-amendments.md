# ADR 0015: Amendments made while implementing authentication

- Status: Accepted
- Date: 2026-11-10

## Context

ADR 0013 and ADR 0014 and `docs/auth-design.md` were written before the code.
Building the feature showed five places where the text was incomplete or wrong.
This ADR records what the code does now. Each item was checked against the code.

## Decision

### 1. Rate-limit key, expiry and fail-open (amends ADR 0013)

- Key: `ratelimit:<policy>:<client-ip>:<window-start-unix-seconds>`, policy
  `global` or `auth` (the stricter limit of `login` and `register`). ADR 0013 said
  `ratelimit:<ip>:<window-start>`.
- The Lua script sets the expiry with `PEXPIRE` (milliseconds), not `EXPIRE`, on
  the first increment only.
- Any Redis error (connection, timeout, server error such as LOADING or READONLY)
  fails open, not only an unreachable server. After a failure, Redis is skipped
  for the rest of that window and the per-instance in-memory limiters decide;
  the next window tries Redis again. One warning is logged per window.

### 2. Forwarded headers (amends `docs/auth-design.md` section 11)

- With both trusted lists empty, the framework's forwarded headers middleware
  accepts the headers from ANY sender. The design said that cleared defaults mean
  "trust nobody"; that was wrong. With an empty `ForwardedHeaders:KnownProxies`
  the middleware is therefore not added at all.
- `X-Forwarded-For` and `X-Forwarded-Proto` are both honoured, only from the
  addresses in `KnownProxies` (`ForwardedHeadersOptions.KnownProxies`).
- The network list is `KnownIPNetworks`; `KnownNetworks` is obsolete in .NET 10
  (ASPDEPR005). It is cleared and not used.
- Startup fails when `ASPNETCORE_FORWARDEDHEADERS_ENABLED` or
  `FORWARDEDHEADERS_ENABLED` is true: the host would then trust every sender.
  Proxies are listed in `ForwardedHeaders:KnownProxies` instead.

### 3. NullGuard gap test exempts framework overrides (clarifies ADR 0008)

`NullGuardGapTests.Src_HasNoNonNullableReferenceValueTask` ignores overrides of
members declared outside src, because their signature is fixed by the framework
(example: `PartitionedRateLimiter<T>.AcquireAsyncCore` returns
`ValueTask<RateLimitLease>`). A control test proves that such an override is
ignored. Everything else still fails the test. The NullGuard mode and flags of
ADR 0008 are unchanged.

### 4. No anonymous Sample endpoints (completes ADR 0014 decision 1)

The `Samples` exemption for `[AllowAnonymous]` is removed: every Sample action is
`[Authorize]`. The only anonymous actions are the four `AuthController` actions
(`Register`, `Login`, `Refresh`, `Logout`).

### 5. Any unique-index violation returns `persistence.duplicate_key`

`IUnitOfWork.SaveChangesAsync` maps a unique-index or unique-constraint violation
(SQL Server error 2601 or 2627) to a 409 `Conflict` result with the code
`persistence.duplicate_key`, whichever index it hits. Callers translate it:
register maps it to `users.email_taken`; the admin seeder uses it to detect an
email conflict.

## Consequences

- ADR 0013 and ADR 0014 carry a short "Amended by ADR 0015" note at the affected
  lines; their decisions are not rewritten.
- An empty `ForwardedHeaders:KnownProxies` means the client address is the direct
  peer address; behind a proxy the list must be filled.
- A Redis outage costs at most one failed call per rate-limit window and instance.
- A unique violation on a table other than `Users` surfaces as 409
  `persistence.duplicate_key` unless the handler translates it.
