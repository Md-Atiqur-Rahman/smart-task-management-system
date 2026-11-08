# ADR 0012: Transitive security pin for System.Security.Cryptography.Pkcs

- Status: Accepted
- Date: 2026-11-08

## Context

NuGetAudit runs on every restore. With `TreatWarningsAsErrors`, its warnings fail
the build; this is the repository's strict vulnerability gate.

Adding EF Core SQL Server failed the build:

```
warning NU1903: Package 'System.Security.Cryptography.Pkcs' 9.0.11 has a known high severity vulnerability, https://github.com/advisories/GHSA-846f-f764-76mx
warning NU1902: Package 'System.Security.Cryptography.Pkcs' 9.0.11 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-2fmv-rv2c-59x7
```

Dependency chain (`dotnet nuget why`):
`Microsoft.EntityFrameworkCore.SqlServer 10.0.14` -> `Microsoft.Data.SqlClient 6.1.7`
-> `System.Security.Cryptography.Pkcs 9.0.11`.

| Advisory | Severity | Affected | Patched |
| --- | --- | --- | --- |
| GHSA-846f-f764-76mx (CVE-2026-84008, .NET DoS) | high | 9.0.0-9.0.20, 10.0.0-10.0.12 | 9.0.21, 10.0.13 |
| GHSA-2fmv-rv2c-59x7 (CVE-2026-70162, .NET DoS) | moderate | 9.0.0-9.0.20, 10.0.0-10.0.12 | 9.0.21, 10.0.13 |

Even the newest `Microsoft.Data.SqlClient` (7.1.1) only requires
`System.Security.Cryptography.Pkcs >= 9.0.18`, still in the vulnerable range, so
upgrading SqlClient does not fix it.

## Decision

- Enable Central Package Management transitive pinning
  (`CentralPackageTransitivePinningEnabled=true` in `Directory.Packages.props`).
- Pin `System.Security.Cryptography.Pkcs` to **10.0.14** (latest stable, patched,
  same vendor and package, matches the .NET 10 runtime line).
- Proof: `dotnet nuget why` now shows
  `Microsoft.Data.SqlClient (v6.1.7) -> System.Security.Cryptography.Pkcs (v10.0.14)`,
  and the build has 0 NuGetAudit warnings.
- The gate stays strict: no `NoWarn` / `WarningsNotAsErrors` for NU190x.

## When to remove

Remove the `PackageVersion` pin (and this ADR's status becomes Superseded) when
`dotnet nuget why src/Infrastructure/Infrastructure.csproj System.Security.Cryptography.Pkcs`
shows a patched version WITHOUT the pin, i.e. when EF Core / SqlClient themselves
depend on >= 9.0.21 or >= 10.0.13. Check on every EF Core upgrade. Keep
transitive pinning enabled for future security pins.

## Consequences

- One extra line in `Directory.Packages.props`, documented in place.
- A newer major (10.x) of a transitive package than SqlClient asks for (9.x);
  allowed by its `>=` range. Covered by the integration tests against real SQL
  Server.
