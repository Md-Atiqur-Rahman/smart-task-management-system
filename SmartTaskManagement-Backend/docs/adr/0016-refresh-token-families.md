# ADR 0016: Refresh-token families; reuse detection revokes one family

- Status: Accepted
- Date: 2026-11-10

## Context

ADR 0014 decision 5 revoked **all** of a user's active refresh tokens when a
revoked token was presented outside the grace period. The whole-branch review
found two problems with that scope:

- Tokens are also revoked by a role change and by an earlier revoke-all. When the
  devices of one user later presented those old cookies, each presentation
  revoked the sessions the other devices had just started again: the devices
  signed each other out in a loop.
- Anyone holding any old revoked token of the user (a leaked log line, an old
  backup of a browser profile) could sign the user out on every device at will.

The developer decided on 2026-11-10 to limit reuse detection to the token's
family.

## Decision

### 1. Every refresh token belongs to a family

`RefreshToken.FamilyId` (required, indexed) identifies the chain of tokens
started by one login or registration.

- `RefreshToken.Issue(...)` (login, registration) starts a new family. The family
  is named after its first token: `FamilyId` = that token's `Id`. Chosen over a
  separate `Guid.CreateVersion7()` because it needs no second id, keeps the first
  token of a chain recognisable, and matches the backfill of existing rows.
- `RefreshToken.IssueInFamily(..., familyId)` (rotation) issues the next token in
  the same family. It has the same guard clauses as `Issue` and rejects an empty
  family id.
- Migration `AddRefreshTokenFamilies` backfills existing rows with
  `FamilyId = Id`: each old token becomes its own family.

### 2. Reuse detection revokes only the presented token's family (amends ADR 0014 decision 5)

When a revoked token is presented outside the grace period (including a token
revoked by logout or by a role change), all active tokens **of that token's
family** are revoked with one set-based `UPDATE`
(`IRefreshTokenRepository.RevokeActiveInFamilyAsync`), executed immediately and
not through `IUnitOfWork` (the reason of ADR 0014 decision 5 is unchanged). The
response is 401 `auth.invalid_refresh_token` and the cookie is deleted.

- When the update revoked at least one token, a warning is logged with the user
  id, the family id and the count (never a token value).
- When it revoked nothing (the family is already dead, for example after a role
  change or a logout), an Information entry is logged instead; there is nothing
  to protect.

### 3. Unchanged

- The grace period: a token revoked by rotation less than
  `RefreshToken:ReuseGraceSeconds` ago returns 401 `auth.refresh_superseded`,
  revokes nothing and keeps the cookie.
- A role change still revokes **all** of the user's active refresh tokens (load,
  `Revoke`, one save), across every family.

## Consequences

- Other devices stay signed in when one device's chain is replayed; a stolen
  chain is still cut off as a whole.
- An old revoked token can no longer sign the user out elsewhere, so presenting
  it is harmless.
- If a thief and the victim share one family, both are signed out of that family
  only; other devices of the user are not affected and are not protected by this
  event either (they were never part of the stolen chain).
- ADR 0014 carries a short "Amended by ADR 0016" note at decision 5; its text is
  not rewritten. ADR 0015 has no line about reuse detection and is unchanged.
