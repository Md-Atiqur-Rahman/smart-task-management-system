# Security rules

- Identity only from the validated JWT; `SecurityContextMiddleware` is the only
  caller of `ISecurityContextSetter.SetPrincipal` (ADR 0007). Inbound claim
  mapping is off; claim names only from `SecurityClaimTypes`.
- Every controller action declares `[Authorize]` or `[AllowAnonymous]`; there is
  no fallback policy. `[AllowAnonymous]` is allowed only on exactly four
  actions, `AuthController.Register`, `Login`, `Refresh` and `Logout` (explicit
  allow-list in architecture test 5, ADR 0014), and, until the Sample slice is
  removed (Task 14), in the `Samples` namespace with the comment
  `// SAMPLE ONLY: replace with [Authorize] in Phase 2`.
- Refresh and logout carry `[RequireAllowedOrigin]` (CSRF check against
  `Cors:AllowedOrigins`); the refresh token lives only in the HttpOnly cookie.
- Minimal public attack surface: only controllers and their request/response
  types are public in Api (CA1515 scoped exception + architecture test
  `PublicTypesInApi_OnlyInControllersOrContracts`, see anti-patterns.md).
- Secrets never in tracked files: user-secrets locally, environment variables
  elsewhere; `.env.example` holds safe dev-only placeholders only.
- Database TLS: `Encrypt=false` only in Development/Testing; the
  certificate-trust keyword is forbidden (ADR 0011, checked by `scripts/verify`).
- The local SQL Server container binds to `127.0.0.1` only.
- Swagger UI and the OpenAPI endpoint are served in Development only.
