# Architecture Decision Records

Changing an accepted decision needs a new ADR that supersedes the old one.

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-cqrs-lite.md) | CQRS Lite with own handler interfaces | Accepted |
| [0002](0002-read-repositories.md) | Read repositories instead of IQueryable in Application | Accepted |
| [0003](0003-automapper-dropped.md) | AutoMapper dropped; hand-written projections | Accepted |
| [0004](0004-result-exception-hybrid.md) | Result for expected failures, exceptions for unexpected ones | Accepted |
| [0005](0005-problemdetails-without-wrapper.md) | Raw DTOs on success, ProblemDetails on errors, no envelope | Accepted |
| [0006](0006-password-hasher.md) | ASP.NET Core Identity PasswordHasher behind IPasswordHasher | Accepted |
| [0007](0007-security-context.md) | Security context set once by middleware, implemented in Application | Accepted |
| [0008](0008-nullguard.md) | NullGuard.Fody with Mode=NullableReferenceTypes | Accepted |
| [0009](0009-openapi-check.md) | Committed docs/openapi.json, generated at build time and checked by verify | Accepted |
| [0010](0010-two-di-styles.md) | Two DI styles, decided by project | Accepted |
| [0011](0011-database-tls.md) | Database TLS: Encrypt=false only in Development and Testing | Accepted |
| [0012](0012-transitive-security-pins.md) | Transitive security pin for System.Security.Cryptography.Pkcs | Accepted |
| [0013](0013-redis-shared-state.md) | Redis for shared security state across instances | Accepted |
| [0014](0014-auth-endpoints-and-redis-amendments.md) | Auth endpoints and amendments to the Redis design | Accepted |
| [0015](0015-auth-implementation-amendments.md) | Amendments made while implementing authentication | Accepted |
| [0016](0016-refresh-token-families.md) | Refresh-token families; reuse detection revokes one family | Accepted |
| [0017](0017-email-outbox.md) | Email through a transactional outbox on the notification row | Accepted |
| [0018](0018-sse-and-redis-pubsub.md) | Live events over SSE, fanned out through Redis pub/sub (amends 0013) | Accepted |
| [0019](0019-repository-layout.md) | Repository layout with separate backend and frontend folders | Accepted |
| [0020](0020-ai-provider.md) | Google Gemini API as the AI provider | Accepted |
