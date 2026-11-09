# ADR 0014: Auth endpoints and amendments to the Redis design

- Status: Accepted
- Date: 2026-11-10

## Context

ADR 0013 fixed the token version design. Building the authentication feature
(`docs/auth-design.md`) showed five points where the first text needs an
amendment or where a new rule is needed. The developer approved the design on
2026-11-09; decision 5 was made during implementation on 2026-11-10.

## Decision

### 1. Four anonymous auth actions, by explicit allow-list

Exactly four actions may be anonymous in the whole Api: `AuthController`
`Register`, `Login`, `Refresh` and `Logout`. Architecture test 5 checks an
explicit allow-list of these four (implemented with the Auth controller). The
exemption for the `Samples` namespace is removed when the Sample slice is removed.
Every other action uses `[Authorize]`.

### 2. Version check in a middleware, not in `OnTokenValidated` (amends ADR 0013)

The check runs in `TokenVersionMiddleware`, right after `UseAuthentication` and
before `SecurityContextMiddleware`. Reason: fail closed must return 503, and an
exception thrown inside the JWT bearer events becomes an authentication failure
(401). The middleware skips endpoints with `IAllowAnonymous` metadata and
unauthenticated requests. A token without a valid `sub` or integer `tv` claim, or
with a version behind the current one, gets 401 `auth.token_revoked`; an
unreachable store gets 503 `auth.unavailable`.

### 3. Role change writes Redis before the database (amends ADR 0013)

The new version is written to Redis first (forward-only). If that fails the
request returns 503 and nothing is saved. Then the user and the revoked refresh
tokens are saved in one `SaveChangesAsync`. If the save fails, Redis holds the new
version while the database holds the old one; this fails safe (old tokens are
rejected, the Admin retries). This removes the window of ADR 0013's order
(commit first), where a failed Redis write and a failed key delete could leave old
tokens valid for up to 10 minutes.

### 4. Fixed key expiry instead of sliding (amends ADR 0013)

A version key expires one hour after the last write or load and a read never
extends it. A miss reloads from SQL Server. Reason: a sliding expiry needs a write
on every read and keeps active users' keys alive indefinitely, so a stale value
could never age out.

### 5. Reuse detection revokes the token family with one immediate UPDATE

When a revoked refresh token is presented outside the grace period, all of the
user's active refresh tokens are revoked with one set-based `UPDATE` that executes
immediately, not through `IUnitOfWork`. Reason: a `RowVersion` conflict in a
batched `SaveChanges` would roll back every revocation silently, and the theft
response must not depend on a concurrent rotation losing or winning.

## Consequences

- `AuthController` is the only anonymous controller; the architecture test and its
  allow-list must change with any new anonymous action.
- Middleware order matters: authentication, token version, security context,
  authorization.
- A role change needs Redis available; during a Redis outage it returns 503.
- Redis keys disappear one hour after the last write even for active users; the
  next request pays one SQL query.
- Reuse detection bypasses the unit of work on purpose; the update is atomic in
  SQL Server.
