# Design: Authentication and authorization

- Status: Draft
- Date: 2026-11-09
- Requirements: `docs/PRD.md` sections 2-7 (this document does not repeat them;
  "PRD 5" means PRD section 5). Decisions: ADR 0013 (Redis), ADR 0006 (password
  hasher), ADR 0007 (security context).
- Marked **[builder's call]**: decisions made by the agent while writing this
  design, not by the developer. Change them freely if a better option appears.

## 1. Scope

In scope: users, roles, register, login, refresh, logout, token version check,
Admin user list and role change, assignable-user search, first-Admin seeding,
forwarded headers, the Redis pieces of ADR 0013, and the Angular auth contract.

Out of scope here: per-resource visibility of projects and tasks (PRD 2-3). That
logic lives in the Project and Task modules; this design only provides what they
need (`ISecurityContext` with user id and role). Deactivation and password change
are out of scope for the first version (PRD 5).

## 2. Domain (src/Domain/Users)

### `Role` (enum)

`Admin`, `ProjectManager`, `TeamMember`. The JWT `role` claim carries the enum
name (`"Admin"`, `"ProjectManager"`, `"TeamMember"`); display names ("Project
Manager") are a frontend concern.

### `User` aggregate (`AuditableAggregateRoot<Guid>`)

| Field | Rule |
| --- | --- |
| `Email` | Value object: trimmed, lower-case, valid format, max 256; unique (database index) |
| `FullName` | Required, trimmed, max 100 |
| `PasswordHash` | From `IPasswordHasher` (ADR 0006); never leaves the aggregate through a DTO |
| `Role` | Starts as `TeamMember` |
| `TokenVersion` | `int`, starts at 1, only increases |

Behaviour:

- `User.Register(email, fullName, passwordHash)`: role `TeamMember`, version 1.
- `User.CreateSeedAdmin(email, fullName, passwordHash)`: role `Admin`, version 1.
- `User.ChangeRole(Role newRole)` returns `Result`: same role -> success with no
  change (no version increment) **[builder's call]**; otherwise sets the role and
  increments `TokenVersion`.
- "Last Admin" rule **[builder's call]**: the role-change handler refuses to
  change the role of the only remaining Admin (`users.last_admin`, 409), so the
  system can never lose its last Admin. It is a handler check (needs a count over
  users), not an aggregate invariant.

### `RefreshToken` aggregate (`AggregateRoot<Guid>`, separate from `User`)

Separate aggregate because a user has many tokens and revoking all of them must
not load the user **[builder's call]**.

| Field | Rule |
| --- | --- |
| `UserId` | Reference by id |
| `TokenHash` | SHA-256 of the raw token, hex; unique index |
| `CreatedAt`, `ExpiresAt` | From `TimeProvider`; lifetime from Options (7 days) |
| `RevokedAt` | Null while active |
| `ReplacedById` | Id of the token issued by rotation (audit trail) |
| `RowVersion` | Optimistic concurrency (PRD 7, refresh race) |

Behaviour: `Issue(userId, tokenHash, now, lifetime)`, `Revoke(now, replacedById?)`
returns `Result` (`auth.invalid_refresh_token` if already revoked), `IsActive(now)`.

The raw token is 32 bytes from `RandomNumberGenerator`, base64url encoded. Only
the hash is stored (PRD 5). No salt: the value is random and high-entropy.

Write repositories (Domain): `IUserRepository` (get by id, get by email, add,
count admins), `IRefreshTokenRepository` (get by hash, add, list the active
tokens of a user as tracked aggregates).

"Revoke all of a user's refresh tokens" loads the active tokens and calls
`Revoke` on each, so the revocation is saved in the **same `SaveChangesAsync`**
as the rest of the operation (atomic, no transaction API needed on
`IUnitOfWork`). A user has few active tokens (one per device), so loading them is
cheap. No set-based update **[builder's call]**.

## 3. Endpoints (src/Api/Controllers/Auth, src/Api/Controllers/Users)

| Endpoint | Access | Success | Failures |
| --- | --- | --- | --- |
| `POST /api/auth/register` | Anonymous | 201 + `AuthResponse` + refresh cookie (signed in right away, like the prototype) | 400 validation; 409 `users.email_taken` |
| `POST /api/auth/login` | Anonymous | 200 `AuthResponse` + refresh cookie | 400; 401 `auth.invalid_credentials` (same for unknown email and wrong password) |
| `POST /api/auth/refresh` | Anonymous (cookie) | 200 `AuthResponse` + new refresh cookie | 401 `auth.invalid_refresh_token` (cookie cleared); 403 `auth.origin_not_allowed` |
| `POST /api/auth/logout` | Anonymous (cookie) | 204, cookie cleared; idempotent (no cookie or unknown token is still 204) | 403 `auth.origin_not_allowed` |
| `GET /api/auth/me` | Authenticated | 200 `CurrentUserDto` | 401 |
| `GET /api/users?search=&role=&sortBy=&sortDirection=&page=&pageSize=` | Admin | 200 `PagedResult<UserDto>` (id, full name, email, role, created at) | 400 |
| `PUT /api/users/{id}/role` body `{ "role": "ProjectManager" }` | Admin | 204 | 400; 404 `users.not_found`; 409 `users.last_admin`; 409 `concurrency.conflict` |
| `GET /api/users/assignable?search=&page=&pageSize=` | Admin, ProjectManager | 200 `PagedResult<AssignableUserDto>` (id, full name, email only) | 400 |

- `AuthResponse`: `{ accessToken, expiresAt, user: CurrentUserDto }`.
  `CurrentUserDto`: `{ id, fullName, email, role }`. The refresh token is **never**
  in a response body (PRD 5).
- `assignable` results: for a PM, all Team Members plus the PM themselves (PRD 2:
  a PM may assign to themselves); for an Admin, all users (PRD 2: "to anyone").
  Search matches name or email.
- `/me` lets the Angular app show the user after a silent refresh **[builder's
  call]** (the refresh response already contains the user, so `/me` is a
  convenience; drop it if unused).
- Users list sort whitelist: `fullName`, `email`, `role`, `createdAt`.
- 401 vs 404: these endpoints only return 404 for an Admin looking up a missing
  user; visibility-based 404 (PRD 3) is a Project/Task concern.

### Anonymous endpoints and architecture test 5

Register, login, refresh and logout must be anonymous, but architecture test 5
(`ControllerAuthorizationTests.AllowAnonymous_OnlyInSampleNamespace`) and
`.claude/rules/security.md` allow `[AllowAnonymous]` only in the Sample
namespace. Change: the test gets an explicit allow-list of exactly these four
actions (by controller and action name), and security.md is updated to match.
Any other `[AllowAnonymous]` still fails the build. Recorded in a new ADR
(0014).

The Sample endpoints switch to `[Authorize]` in this work (Phase 2 checklist in
`docs/sample-flow.md`, steps 1-2, developer confirmed), and the Sample namespace
exemption is removed from the test. After this work the four auth actions are the
only anonymous actions. Sample integration and smoke tests get a token from a
shared test helper (also used later by the Project and Task tests). Sample itself
is deleted once the Project module exists.

## 4. Tokens

### Access token (JWT)

- HS256 with the existing `Jwt:SigningKey` (min 32 chars), `Jwt:Issuer`,
  `Jwt:Audience`; lifetime `Jwt:AccessTokenMinutes` = 10 (PRD 5).
- Claims: `sub` (user id), `role`, `tv` (token version), `jti`, `iat`, `exp`.
  Add `TokenVersion = "tv"` to `SecurityClaimTypes` (the only place claim names
  live).
- Created by `ITokenService` (Application interface, Infrastructure
  implementation). The JWT-writing library and version must be verified with
  `dotnet package search` before use (UNVERIFIED: whether Infrastructure needs a
  direct package reference or `JsonWebTokenHandler` comes through an existing one).
- `JwtOptions` is used for both issuing (Infrastructure) and validating (Api);
  move it to Infrastructure configuration so both layers share one validated
  options class **[builder's call]**.

### Refresh token cookie

| Attribute | Value |
| --- | --- |
| Name | `__Secure-refresh_token` **[builder's call]** (the `__Host-` prefix needs `Path=/`, which PRD 5 rules out) |
| `HttpOnly`, `Secure` | Yes |
| `SameSite` | `Strict` |
| `Path` | `/api/auth` (covers refresh and logout; PRD 5) |
| `Expires` | The token's `ExpiresAt` |

Deployment constraint (ASSUMPTION, follows from `SameSite=Strict`): the Angular app
and the API are served from the **same site** (same registrable domain; different
subdomains or ports are fine). Cross-site deployment would need `SameSite=None`
and a new decision. Angular sends the cookie with `withCredentials: true` on the
auth calls only.

UNVERIFIED: browser behaviour of a `Secure` / `__Secure-` cookie on
`http://localhost`; run local development over the existing https launch profile
if the cookie is dropped.

### CSRF check (PRD 5, PROPOSED there)

Refresh and logout reject a request whose `Origin` header is present and not one
of `Cors:AllowedOrigins` (403 `auth.origin_not_allowed`). A missing `Origin`
(Swagger, Postman, curl) is allowed: `SameSite=Strict` already stops browsers
from sending the cookie cross-site **[builder's call]**.

## 5. Flows

### Register

Validate -> email taken? 409 -> hash password -> `User.Register` -> save -> issue
tokens (as login).

### Login

Find user by normalized email -> verify password with `IPasswordHasher` (verify a
dummy hash when the user is unknown, so timing does not reveal whether the email
exists) **[builder's call]** -> 401 `auth.invalid_credentials` on failure ->
issue access token with the user's current `TokenVersion` -> issue refresh token
-> save -> make sure Redis holds the current version (forward-only write, ADR
0013) -> 200 + cookie.

### Refresh (rotation and reuse detection, PRD 5)

1. Read the cookie; missing -> 401.
2. Hash it, load the token by hash. Not found or expired -> 401, clear cookie.
3. Already revoked:
   - **Revoked by rotation less than 30 seconds ago** (grace period, PRD 5): a
     second tab or a network retry, not theft. Return 401
     `auth.refresh_superseded`; revoke nothing; **do not clear the cookie** (the
     browser already holds the new token from the winning request). The client
     retries the refresh once with the cookie it now has. No tokens are issued
     for the old value, so a thief inside the window gets nothing (stricter than
     Okta/Auth0, which issue tokens during their grace period).
   - Otherwise -> **reuse**: revoke all of the user's refresh tokens, log a
     warning (user id, no token value), 401 `auth.invalid_refresh_token`, clear
     cookie.
   - The window length is an option (`RefreshTokenReuseGraceSeconds`, default
     30, allowed 0-60), measured from `RevokedAt` of a token revoked by rotation
     (`ReplacedById` set). A token revoked by logout or by revoke-all has no
     grace.
4. Active -> load the user; revoke this token (`ReplacedById` = new token), issue
   a new refresh token and a new access token with the user's current role and
   version; save. A `RowVersion` conflict (another instance rotated the same token
   at the same moment, PRD 7) -> 401 for the loser.

Why the grace period (developer confirmed): browsers restore several tabs at
once, and each tab refreshes on start with the same cookie. Without a grace
period the second tab would trigger reuse detection and sign the user out on
every device. The grace period is documented vendor practice (Okta "Grace period
for token rotation", default 30 s, range 0-60 s; Auth0 "Rotation Overlap
Period"); RFC 9700 section 4.14 describes rotation without one. The Angular app
also keeps at most one refresh in flight per tab.

### Logout (per device, PRD 5)

Hash the cookie -> if an active token exists, revoke it -> clear the cookie ->
204. No `TokenVersion` change.

### Role change (PRD 5, ADR 0013)

Order: **Redis first, then the database** **[builder's call]**, so a failure
can never leave old tokens working:

1. Admin only -> load user (404) -> last-Admin check (409) -> `ChangeRole`
   (version N -> N+1, in memory only).
2. Forward-only write of N+1 to Redis. If it fails -> 503 `auth.unavailable`,
   **nothing is saved**; the Admin retries later. Old tokens are already
   rejected wherever Redis answers, because Redis can only move forward.
3. Load the user's active refresh tokens and revoke them; one
   `SaveChangesAsync` saves the user and the tokens together.
4. If step 3 fails (database error, concurrency conflict): Redis holds N+1 while
   the database still holds N. This fails **safe**: old tokens (N) are rejected,
   and a fresh login (token N) is also rejected until the Admin retries (the
   retry is a real change again, commits N+1, and both agree) or the Redis key
   expires (fixed expiry, section 9). Error returned: the database error's
   normal mapping (409 for a conflict, 500 otherwise).

Why not database first (the order in ADR 0013): if the Redis write and the key
delete both failed after the commit, Redis kept the old version, and once Redis
answered again the old tokens would pass for up to 10 minutes. Redis first
removes that window. Amended in ADR 0014.

Reuse detection (refresh step 3) revokes all tokens the same way (load, revoke,
one save); it does not touch `TokenVersion`.

### Version check on every authenticated request (ADR 0013)

Change from ADR 0013: the check runs in a **middleware right after
`UseAuthentication`**, not in `JwtBearerEvents.OnTokenValidated`. Reason:
fail-closed needs a 503, and an exception thrown inside the JWT bearer events is
turned into an authentication failure (401) by the handler (UNVERIFIED: confirm
in the ASP.NET Core source/docs before building; if the event can produce a 503
cleanly, keep the ADR text). The change is recorded as an amendment in ADR 0014.

The middleware:

1. Skips anonymous endpoints (endpoint metadata `IAllowAnonymous`) and
   unauthenticated requests **[builder's call]**, so a stale header on login does
   not turn into a 503.
2. Reads `sub` and `tv`. Missing `tv` -> 401.
3. `ITokenVersionStore.GetAsync(userId)`; on a miss, loads `TokenVersion` from the
   database through a read repository and stores it (forward-only).
4. Mismatch -> 401 ProblemDetails `auth.token_revoked`. Redis unreachable -> 503
   ProblemDetails `auth.unavailable`.
5. Only then `SecurityContextMiddleware` sets the principal (ADR 0007), so
   handlers never see a revoked identity.

## 6. Authorization

- Named policies in the Api: `AdminOnly` (role Admin), `ProjectManagerOrAdmin`
  (roles Admin, ProjectManager); plain `[Authorize]` for any signed-in user. Role
  names come from `Role` (no string literals in attributes).
- Ownership and assignment rules (PRD 2-3) are enforced in Application handlers
  and read repositories of the Project and Task modules using `ISecurityContext`;
  not part of this design.
- `ISecurityContext` gains `Role? Role` (single role, PRD 2) next to the existing
  `Roles` collection, or `Roles` is replaced **[builder's call]**.

## 7. Validation (FluentValidation)

- Email: required, valid format, max 256.
- Full name: required, max 100.
- Password (register): 8-128 characters, at least one letter and one digit (the
  prototype's rule) **[builder's call on the upper limit]**.
- Login: email and password required only (no policy hints on login).
- Role change: role must be a defined `Role` value.

## 8. First-Admin seeding (PRD 4)

- Options `AdminSeed` (`Email`, `Password`, `FullName`), only from user-secrets or
  environment variables. All three missing -> seeding skipped (information log).
  Some but not all present, or a password that breaks the policy -> startup fails
  (`IValidateOptions`, `ValidateOnStart`).
- A hosted service at startup: if any Admin exists, skip; else create the seed
  Admin.
- On a unique-email violation, check again whether an Admin now exists:
  - yes -> a parallel instance won the race (PRD 7); log and continue;
  - no -> the seed email belongs to an existing non-Admin account. **Startup
    fails** with a clear error **[builder's call]**. The account is never
    promoted (whoever registered that email first would become Admin: account
    takeover). Recovery: set a different `AdminSeed:Email` and restart. The
    README documents this.
- Seeding needs the schema: in Development it runs after the existing
  development migration. Outside Development, migrations are applied before the
  app starts using an **idempotent SQL script** (`dotnet ef migrations script
  --idempotent`) documented in the README; this work does not build an automatic
  migration runner **[builder's call]**. If the schema is missing, seeding fails
  and startup fails (fail fast).
- Consequence of "no password change" (PRD 5): the seeded Admin's password can
  only be changed by a future feature. Documented in the README.

## 9. Redis pieces (ADR 0013)

- `RedisOptions` (`ConnectionString` secret, `ConnectTimeoutMs`,
  `OperationTimeoutMs`); one `IConnectionMultiplexer` singleton in Infrastructure.
- `ITokenVersionStore` (Application interface): `GetAsync`, `SetIfHigherAsync`,
  `RemoveAsync`; Lua compare-and-set; key `auth:token-version:<userId>`.
- Expiry: **fixed**, set when the key is written or loaded, not refreshed on
  read; length in options (default 1 hour) **[builder's call]**. A sliding expiry
  (ADR 0013's text) would keep a wrong value alive forever while the user keeps
  making requests; a fixed expiry bounds the rare "Redis ahead of database" case
  of section 5 to one hour. Amended in ADR 0014.
- Shared rate limit counter and Redis `PING` health check exactly as in ADR 0013.
- Integration tests run a Redis container from `REDIS_IMAGE`; whether to use a
  `Testcontainers.Redis` package or the generic container builder is verified
  when building (UNVERIFIED).

## 10. Rate limiting

- The global per-IP limit stays (ADR 0013, shared through Redis).
- A stricter limit on `login` and `register`: 10 requests per minute per IP, in
  `RateLimitingOptions` **[builder's call]**. Same Redis counter, separate key
  prefix; same fail-open fallback.

## 11. Forwarded headers (PRD 7)

- `ForwardedHeadersOptions`: `X-Forwarded-For` and `X-Forwarded-Proto`, trusted
  proxies from `ForwardedHeaders:KnownProxies` (list of IP addresses), empty by
  default. The framework's default trusted entries (loopback) are cleared, so an
  empty list really means "trust nobody".
- `UseForwardedHeaders` runs first in the pipeline, before HTTPS redirection,
  logging and rate limiting.
- UNVERIFIED: exact property names in .NET 10 (`KnownProxies`,
  `KnownNetworks` / `KnownIPNetworks`); check the official docs when building.

## 12. Logging

- Every log entry carries `InstanceId` (PRD 7): from configuration
  `Instance:Id`, defaulting to the machine name, added as a Serilog property
  without an extra enricher package **[builder's call]**.
- Never log passwords, tokens, cookie values or token hashes. Auth events
  (login failure, reuse detection, role change) are logged with user id and
  client IP only.

## 13. Angular contract (for the later frontend phase, PRD 11)

- Access token in memory only; refresh cookie handled by the browser (PRD 5).
- On app start: `POST /api/auth/refresh` with credentials; success -> signed in;
  401 `auth.refresh_superseded` -> retry the refresh once; any other 401 -> login
  screen.
- Interceptor: attaches the bearer token to API calls; on 401 runs one refresh
  (shared by concurrent requests) and retries once; a second 401 -> login screen.
  503 `auth.unavailable` -> a "service unavailable" message, no sign-out.
- Guards: signed in; role guards for Admin pages (user list) and PM/Admin pages.

## 14. Errors (ProblemDetails `errorCode`)

| Code | Status |
| --- | --- |
| `auth.invalid_credentials` | 401 |
| `auth.invalid_refresh_token` | 401 |
| `auth.refresh_superseded` | 401 (grace period; client retries once) |
| `auth.token_revoked` | 401 |
| `auth.origin_not_allowed` | 403 |
| `auth.unavailable` | 503 |
| `users.email_taken` | 409 |
| `users.not_found` | 404 |
| `users.last_admin` | 409 |
| `concurrency.conflict` | 409 (existing) |
| `rate_limited` | 429 (existing) |

## 15. Tests

- Unit: `User` (register, change role, version), `RefreshToken` (revoke twice),
  validators, handlers with NSubstitute (login failure paths, reuse detection,
  last Admin).
- Integration (SQL Server + Redis containers): register/login/refresh/logout
  round trip with the cookie; reuse detection; reuse inside the grace period
  (401 `auth.refresh_superseded`, nothing revoked, cookie kept) and after it
  (all revoked); role change makes the old access
  token 401 on the next request; Redis stopped -> 503 on an authenticated
  endpoint and login still works; rate limit 429; forwarded header ignored from
  an untrusted address.
- Architecture: the anonymous allow-list (section 3); existing tests stay green.
- The whole Phase 2 checklist in `docs/sample-flow.md`: Sample endpoints to
  `[Authorize]`, Sample exemption removed, Bearer requirement per operation in
  OpenAPI, tests for no token (401), valid token, wrong role (403), Swagger steps
  updated, `docs/openapi.json` regenerated.

## 16. Documents to update with this work

- New ADR 0014: anonymous auth endpoints allow-list; amendments to ADR 0013
  (version check in middleware instead of `OnTokenValidated`; role change writes
  Redis before the database; fixed instead of sliding key expiry).
- `.claude/rules/security.md`, `docs/sample-flow.md` Phase 2 checklist,
  `docs/CONTEXT.md` (Redis exception, 10-minute token, forwarded headers),
  `.env.example` (`REDIS_IMAGE`, `REDIS_PASSWORD`, seed placeholders),
  `docker-compose.yml` (Redis service), README setup steps.

## 17. Left to the builder

File and class names beyond those above, the exact Lua scripts, the order of
commits, and test names (`Method_Condition_ExpectedResult`).
