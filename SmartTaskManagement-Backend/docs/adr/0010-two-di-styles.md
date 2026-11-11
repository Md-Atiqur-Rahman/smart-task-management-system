# ADR 0010: Two DI styles, decided by project

- Status: Accepted
- Date: 2026-11-08

## Context

Service registration tends to drift into `Program.cs` or into one large
extension class. The layers also differ: Application and Infrastructure are
libraries that should configure themselves from the host builder (they need
`builder.Environment` and `builder.Configuration`), while the Api owns the HTTP
pipeline.

## Decision

- **Application and Infrastructure** use `Configure<Concern><TBuilder>(this
  TBuilder builder) where TBuilder : IHostApplicationBuilder`, returning the
  builder, in `<Layer>/Configuration/<Concern>/HostApplicationBuilderExtensions.cs`.
  Each layer exposes one entry point: `ConfigureApplication()` and
  `ConfigureInfrastructure()`. They need only
  `Microsoft.Extensions.Hosting.Abstractions` (no persistence package in
  Application).
- **Api** uses `Add<Concern>(this IServiceCollection services[, IConfiguration
  configuration])` for services and `Use<Concern>(this WebApplication app)` for
  the pipeline, in `Api/Extensions/<Concern>Extensions.cs`. Configuration is read
  only inside these methods and bound immediately to Options with
  `ValidateOnStart()`.
- `Program.cs` only calls the two entry points and the Api `Add*`/`Use*`
  methods; it registers nothing inline.

## Consequences

- Each concern has one obvious home; the style tells you the layer.
- Architecture tests enforce it: `Configure*` (on `IHostApplicationBuilder`)
  only in Application/Infrastructure and only in `HostApplicationBuilderExtensions`
  classes; `Add*`/`Use*` (on `IServiceCollection`/`WebApplication`) only in the
  Api; no registration API used by `Program` (NetArchTest reads the IL of the
  top-level statements; a deliberate `AddSingleton` in `Program.cs` failed the test).
