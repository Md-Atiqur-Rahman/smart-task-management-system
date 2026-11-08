# ADR 0011: Database TLS: Encrypt=false only in Development and Testing

- Status: Accepted
- Date: 2026-11-08

## Context

TLS certificate validation must never be disabled, in any environment.
The local SQL Server container (Docker Compose) and the Testcontainers SQL Server
use a self-signed certificate. Microsoft.Data.SqlClient defaults to
`Encrypt=Mandatory` and validates the certificate, so connecting to these
containers with encryption needs either a trusted certificate or
`TrustServerCertificate=True`, which is exactly "validation disabled".

The Testcontainers MsSql module's own `GetConnectionString()` adds
`TrustServerCertificate=True` (seen in its source), so it is not used.

## Options considered

1. `Encrypt=False` for the local and test database (no TLS on loopback; validation
   never turned off). **Chosen.**
2. A dev CA certificate mounted into SQL Server and trusted on the host and in CI:
   full TLS, much more setup.
3. `TrustServerCertificate=True` in Development: breaks the rule as written.

## Decision

- The literal `TrustServerCertificate` is forbidden everywhere outside `docs/`.
  `scripts/verify.sh` and `scripts/verify.ps1` fail when it appears
  (`git grep --untracked`, so new files are caught before commit). Proven: a
  temporary file with the literal made both scripts exit 1.
- `DatabaseOptions.Encrypt` (default `true`) maps to
  `SqlConnectionEncryptOption.Mandatory`; `false` maps to `Optional`. The
  connection string is built only by `DatabaseOptions.BuildConnectionString()`
  and never sets the certificate-trust keyword.
- `DatabaseOptionsValidator` (`IValidateOptions`, `ValidateOnStart`) rejects
  `Encrypt=false` in every environment except `Development` and `Testing`.
- `appsettings.json` has `Encrypt: true`; only `appsettings.Development.json` and
  the integration tests (environment `Testing`) set `false`.

## Consequences

- Local and test traffic to SQL Server is unencrypted on the developer machine
  and the CI runner only. Every other environment must use encryption with a
  certificate the client trusts, or the app does not start.
