# Testing rules

- Framework: xunit.v3 on Microsoft.Testing.Platform (`global.json`
  `"test": {"runner": "Microsoft.Testing.Platform"}`), NSubstitute, Shouldly,
  NetArchTest.Rules, Testcontainers (SQL Server). No FluentAssertions, no Moq.
- Run: `dotnet test --solution Datavanced.SmartTaskManagement.slnx` (or
  `--project <csproj>`). Test projects are `OutputType=Exe`.
- Test projects are detected by the explicit `IsTestProject=true` property in each
  test csproj, never by name.
- `tests/UnitTests`: Domain and Application only; never references Infrastructure
  or Api (architecture rule 3).
- `tests/IntegrationTests`: real host through `ApiFactory`
  (`WebApplicationFactory<Program>`, environment `Testing`) and real SQL Server
  through `SqlServerFixture` (image pinned in `.env.example`, connection string
  built by `DatabaseOptions`, never the container's own string).
- `tests/WeavingProbe` is NOT a test project: it is woven and fully analyzed like
  src (no test-only `NoWarn`).

## Analyzer rules disabled for test projects only

Both live in `Directory.Build.targets` under `IsTestProject == 'true'`, with a
reason comment. The global `NoWarn` in `Directory.Build.props` is unchanged.

| Rule | Reason |
| --- | --- |
| CA1707 | Test names follow `Method_Condition_ExpectedResult` |
| CA1515 | xUnit requires public test classes (xUnit1000) and public collection definitions (xUnit1027); fixtures injected into a public test constructor must then be public too |
