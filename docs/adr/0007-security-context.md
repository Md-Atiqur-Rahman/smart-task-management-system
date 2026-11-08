# ADR 0007: Security context set once by middleware, implemented in Application

- Status: Accepted
- Date: 2026-11-08

## Context

Handlers need the current user's identity. Reading `HttpContext` or the token
inside handlers ties Application to HTTP and spreads claim parsing everywhere.
Requirements: identity only from the validated token, a missing claim means
unauthenticated (no default values), claim names only from `SecurityClaimTypes`.

## Decision

- `ISecurityContext` (reader: `IsAuthenticated`, `UserId`, `Roles`) and
  `ISecurityContextSetter` (`SetPrincipal(ClaimsPrincipal)`) are declared in
  Application.
- Both are implemented by one class, `SecurityContext`, also in Application.
  It is pure claims logic (no HTTP, no token parsing), registered scoped once and
  exposed as both interfaces (same instance per request).
  - The plan first placed it in Infrastructure. It moved to Application because it
    has no infrastructure dependency and must be unit-testable, and UnitTests may
    not reference Infrastructure (architecture rule 3).
- `SetPrincipal` may be called once per request; a second call throws.
  The method was named `Set` first; CA1716 flagged it (VB keyword) and it was
  renamed.
- If the principal is not authenticated, or the `sub` claim is missing or not a
  GUID, the context stays unauthenticated with no roles.
- One Api middleware, after authentication, calls `SetPrincipal(HttpContext.User)`.
  Handlers and controllers never call the setter and never read `HttpContext`.
- JWT inbound claim mapping is turned off, so `sub` and `role` arrive with their
  JWT names (`SecurityClaimTypes.UserId = "sub"`, `SecurityClaimTypes.Role = "role"`).
  Proven by an integration test with a real signed token.

## Consequences

- Handlers depend on `ISecurityContext` only and are testable without HTTP.
- Claim names live in one place.
