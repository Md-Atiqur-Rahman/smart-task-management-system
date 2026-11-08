# ADR 0006: ASP.NET Core Identity PasswordHasher behind IPasswordHasher

- Status: Accepted
- Date: 2026-11-08

## Context

Phase 2 (authentication) needs password hashing. Writing a hashing scheme is a
security risk; pulling in the full ASP.NET Core Identity stack (stores,
managers, cookie flows) is more than this API needs.

## Decision

- Application declares `IPasswordHasher` (`Hash(password)`,
  `Verify(passwordHash, providedPassword)`).
- Infrastructure implements it with `PasswordHasher<TUser>` from
  `Microsoft.Extensions.Identity.Core` 10.0.14 (`PasswordHasherAdapter`): salted,
  versioned hashes with the framework's default key-derivation settings. Only
  this hasher is used; no Identity stores, managers or schema.
- `Verify` returns true for `Success` and `SuccessRehashNeeded`. Rehashing on
  login is a Phase 2 decision.

## Consequences

- Hashing follows the maintained framework implementation and its upgrades.
- Application stays independent of the hashing library; handlers can be tested
  with a substitute.
- Tests: hash/verify round trip, wrong password rejected, different salts for the
  same password.
