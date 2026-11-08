# Infrastructure layer rules

- References Application and Domain. The only layer with EF Core and SQL Server.
- `Persistence/`: `AppDbContext`, entity configurations (`ConfigureAuditable`
  for audit fields, soft-delete filter and `RowVersion`), write repositories,
  read repositories (`AsNoTracking` + hand-written `Select` to DTOs, using
  `ApplySearch` / `ApplySort` / `ToPagedResultAsync`), `UnitOfWork`
  (stale `RowVersion` -> `concurrency.conflict`), migrations, `AuditInterceptor`.
- Sorting only through the whitelist map of each read repository; never pass a
  user string to `OrderBy`.
- Connection string only from `DatabaseOptions.BuildConnectionString()`;
  `Encrypt=false` only in Development/Testing; never the certificate-trust keyword.
- Migrations: `dotnet tool restore`, then
  `Database__Name=design-time Database__Password=design-time dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations`.
  Auto-migration runs in Development only (`DevelopmentMigrationService`).
- DI style: `Configure<Concern><TBuilder>(this TBuilder) where TBuilder :
  IHostApplicationBuilder` in `Configuration/<Concern>/HostApplicationBuilderExtensions.cs`
  (Database, Security, Time), called only from `ConfigureInfrastructure()`.
  No `Add*`/`Use*` extension methods.
- Register every new repository and read repository in `ConfigureDatabase`.
