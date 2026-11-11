# Package rules

Paths in this file are relative to the backend folder `<PROJECT_NAME>-Backend/`
(ADR 0019).

- Verify every package and version BEFORE adding it:
  `dotnet package search <id> --exact-match` (latest stable, license, target
  frameworks). Never write a version from memory. If it does not support
  net10.0 or its license is unclear or commercial, stop and ask.
- Versions live only in `Directory.Packages.props` (Central Package
  Management); no `Version` attribute in any csproj.
- Vulnerabilities: NuGetAudit fails restore (warnings are errors), and
  `scripts/check-packages` fails verify on any vulnerable or deprecated package,
  top-level or transitive. Fix by upgrading; for a vulnerable transitive package,
  pin a patched version with CPM transitive pinning and record it in an ADR
  (example: ADR 0012). Never suppress NU1901-NU1904.
- No NuGet lock files (and therefore no CI NuGet cache).
- Not allowed: MediatR, AutoMapper (ADR 0003), FluentAssertions, Moq,
  CSharpier, Specification pattern libraries, caching libraries, API
  versioning, Domain Events libraries, Aspire, StyleCop, Meziantou, AsyncFixer,
  VS.Threading analyzers, commitlint, Renovate, Spectral, SonarQube.
- Design-time packages (`Microsoft.EntityFrameworkCore.Design`,
  `Microsoft.Extensions.ApiDescription.Server`) use `PrivateAssets="all"`.
- Local tools are pinned in `dotnet-tools.json` (backend root): `dotnet tool restore`.
