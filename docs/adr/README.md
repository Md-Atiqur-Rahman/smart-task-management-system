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
