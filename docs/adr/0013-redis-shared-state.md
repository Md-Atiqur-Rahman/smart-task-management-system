# ADR 0013: Redis for shared security state across instances

- Status: Accepted
- Date: 2026-11-09

## Context

The API must run as several instances behind a load balancer. Two security
requirements cannot be met with per-instance memory:

1. **Immediate revocation.** When an Admin changes a user's role, that user's
   existing access tokens must stop working at once, on every instance, not when
   they expire. Access tokens are self-contained JWTs, so
   the server must know which tokens are no longer valid.
2. **No database query per request.** The check above runs on every
   authenticated request, so it cannot read SQL Server each time.

Options considered:

| Option | Immediate | Multi-instance | Per-request cost | Rejected because |
| --- | --- | --- | --- | --- |
| A. Per-instance in-memory revocation state | Yes | No | Dictionary lookup | A change made on instance 1 is unknown to instance 2 |
| B. Token version in a shared store (Redis), SQL Server as source of truth | Yes | Yes | One Redis round trip | **Chosen** |
| C. Very short access tokens (1-2 min) | No | Yes | None | Fails "immediately" |

Polling SQL Server or a message backplane to keep A in sync was not chosen: it is
either not immediate (polling interval) or needs a broker anyway.

`.claude/rules/packages.md` forbids caching libraries. That rule exists to stop
caching for performance, which hides stale data and adds a moving part without a
requirement. Here Redis holds security state that every instance must see the
same way; revocation correctness depends on it. That is the reason for this
exception, and the exception is limited to the uses listed below.

## Decision

### Token version

- `User` has a `TokenVersion` (integer, starts at 1) in SQL Server, the source of
  truth.
- Every access token carries the version as a claim (name defined in
  `SecurityClaimTypes`).
- Access token lifetime: **10 minutes** (bound from validated JWT Options).

### Check on every authenticated request

After JWT signature and lifetime validation (`JwtBearerEvents.OnTokenValidated`):

1. Read the user's current version from Redis (key `auth:token-version:<userId>`).
2. If Redis has no entry, load `TokenVersion` from SQL Server once and store it in
   Redis.
3. If the token's version differs from the current version, authentication fails
   (401).

### Role change (and later: deactivation, password change)

`TokenVersion` increments **only** on these events. In one operation:

1. Increment `TokenVersion` in SQL Server and commit.
2. Write the new version to Redis.
3. Revoke all of the user's refresh tokens (SQL Server), so the user must sign in
   again on every device and receives a token with the new role and version.

### Logout (per device)

- Logout revokes **only the current device's refresh token**. `TokenVersion` is
  not incremented; the user's other devices stay signed in.
- The logged-out device's access token is not revoked; it expires on its own
  within 10 minutes. This is accepted: the client discards it on logout, and the
  window is bounded by the access token lifetime.

### Redis unavailable: fail closed

- If the version check cannot reach Redis, authenticated requests are rejected
  with **503** (ProblemDetails with an `errorCode`), never accepted without the
  check. A demoted user must not regain access during an outage.
- Endpoints that do not need the check (login, register, refresh, `/health`) keep
  working. `/health` reports Redis as unhealthy so the outage is visible.

### Consistency rules (builder's call, recorded here)

- Redis may only move a version **forward**: both the write in step 2 and the
  load-on-miss write use a compare-and-set (Lua script: set only if the stored
  value is missing or lower). This stops a slow load-on-miss that read the old
  version from overwriting a newer one.
- Redis entries have a TTL (sliding, refreshed on load) so inactive users do not
  stay in Redis forever; a miss simply reloads from SQL Server.
- If the Redis write in step 2 fails after the SQL commit, the handler tries to
  delete the key instead (the next request then reloads the new version from SQL
  Server). If that also fails, Redis is down: the request returns an error, not a
  silent success. Old tokens cannot be used meanwhile, because the version check
  fails closed (see above).

### Packages and wiring

- Client: **StackExchange.Redis 3.4.31** (verified 2026-11-09 with `dotnet
  package search StackExchange.Redis --exact-match` and nuget.org: latest stable,
  MIT license, ships a net10.0 build, no vulnerability or deprecation notice).
- The Application layer sees an interface (`ITokenVersionStore`); only
  Infrastructure references StackExchange.Redis.
- The Redis connection is configured through validated Options with
  `ValidateOnStart()`; the connection string is a secret (user-secrets /
  environment variables); TLS rules match ADR 0011 (no TLS only in
  Development/Testing).
- No other Redis package: rate limiting and the health check are hand-written on
  top of StackExchange.Redis (below).

### Rate limiting: shared per-IP limit, fail open

- The existing fixed-window per-IP limit (`RateLimitingOptions`: permit limit,
  window) is shared by all instances through Redis.
- Hand-written counter, no extra package: key
  `ratelimit:<ip>:<window-start>`; one atomic Lua script does `INCR` and, when the
  result is 1, `EXPIRE` for the window length. The request is allowed while the
  count is within the limit; otherwise 429 with the existing `rate_limited`
  ProblemDetails.
- It stays inside the framework's rate limiting middleware (a custom
  `PartitionedRateLimiter<HttpContext>`), so `UseRateLimiter`, `OnRejected` and
  `DisableRateLimiting()` on `/health` keep working.
- **Redis down: fail open.** If the Redis call fails or times out, the request is
  checked against the current per-instance in-memory fixed-window limiter instead.
  A warning is logged. Requests are never blocked just because Redis is down.
- Unlike the token version check, failing open is acceptable here: during an
  outage the limit becomes N times the configured limit (N = instances), which is
  degraded but still bounded.

### Health check

- A hand-written `IHealthCheck` sends Redis `PING`; no extra package.
- Registered on the existing anonymous `/health` endpoint next to the database
  check; Redis down makes `/health` unhealthy.

### Redis image

- Pinned tag **`redis:8.10.3`** (verified 2026-11-09 on the official Docker Hub
  page https://hub.docker.com/_/redis, "Supported tags": `8.10.3, 8.10, 8,
  8.10.3-trixie, ...`; newest listed version, no RC/beta tag listed).
- Single source: `REDIS_IMAGE` in `.env.example`, used by `docker-compose.yml` and
  the integration-test container, like `SQLSERVER_IMAGE`.
- Local container binds to `127.0.0.1` only and requires a password
  (`REDIS_PASSWORD`, dev-only placeholder in `.env.example`).
- License: Redis 8.x is tri-licensed (RSALv2 / SSPLv1 / AGPLv3, per the Docker
  Hub page). The application uses Redis unmodified as a separate network service
  through the MIT-licensed client; no Redis code is distributed with the
  application.

Allowed uses of Redis under this ADR: token versions and the shared rate limit
counter. Any other use (caching query results, sessions, etc.) needs a new ADR.

## Consequences

- One more service to run in every environment (Docker Compose, integration tests
  via Testcontainers, production).
- One Redis round trip on every authenticated request; a SQL query only on a
  Redis miss.
- Role changes take effect on all instances immediately and sign the user out on
  every device.
- Logout is per device; the logged-out device's access token stays valid for at
  most 10 minutes.
- Availability of authenticated endpoints depends on Redis: when Redis is down
  they return 503 (fail closed).
- Rate limiting is shared across instances; when Redis is down it degrades to
  per-instance limits (fail open).
