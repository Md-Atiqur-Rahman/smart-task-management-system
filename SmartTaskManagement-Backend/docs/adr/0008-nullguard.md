# ADR 0008: NullGuard.Fody with Mode=NullableReferenceTypes

- Status: Accepted
- Date: 2026-11-08

## Context

Fody + NullGuard.Fody run on top of `Nullable` enabled, so that
null arguments and null return values fail fast at runtime, not only as compiler
warnings. CA1062 is disabled because NullGuard covers it.

Versions verified on nuget.org: Fody 6.9.3 (MIT), NullGuard.Fody 3.1.1 (MIT,
targets netstandard2.0/2.1). Weaving on net10.0 was proven by the build log:
`Fody/NullGuard: Executing weaver NullGuard v3.1.1`.

## Decision

- One shared `FodyWeavers.xml` at the solution root:
  `<NullGuard Mode="NullableReferenceTypes" />`.
- It is fed to every woven project through the `WeaverConfiguration` MSBuild
  property (`Directory.Build.targets`). Without it, building a single project
  outside the solution made Fody fail and create a per-project
  `FodyWeavers.xml`.
- Flags (`src/SharedAssemblyInfo.cs`):
  `ValidationFlags.Arguments | ValidationFlags.ReturnValues`. Properties and
  non-public members are not checked, because EF Core materializes entities
  through private constructors and setters.
- Woven: Domain, Application, Infrastructure, Api, and `tests/WeavingProbe`
  (probe library for the evidence below). Test projects (`IsTestProject=true`)
  are not woven.

## tests/WeavingProbe

- Purpose: holds probe members (async `Task<T?>`/`ValueTask<T?>`, nullable
  property, generic `T?` return, non-nullable returns) that prove how the woven
  code behaves. The Domain has no such members, and adding test-only members to
  src would put test code in production assemblies.
- Why a woven NON-test library: test projects are not woven (they set
  `IsTestProject=true`), so the probes must live in an assembly that is woven with
  exactly the same `FodyWeavers.xml` and `SharedAssemblyInfo.cs` as src. It does
  not set `IsTestProject`, so `Directory.Build.targets` weaves it like src.
- How verify treats it: built, analyzed (same `AnalysisMode`, warnings as errors),
  format-checked and covered by the placeholder and naming checks like every
  other project. It contains no tests itself; `UnitTests` runs the probes. It is
  never published or put in the Docker image (the Dockerfile publishes Api only).
- How architecture tests treat it: it is not one of the four layers. An
  architecture test (`WeavingProbeTests`) asserts that no src project references it; only `UnitTests`
  may.

## Evidence (real test runs, 2026-11-08)

`Mode=Implicit` (first choice) was rejected. It ignored `?` annotations:
**14/17 unit tests passed**, and these three threw `[NullGuard] ... is null`:

| Parameter | Call |
| --- | --- |
| `string? value` | `Guard.MaxLength(null, 3)` |
| unconstrained `T? value` | `Result.FromNullable<string>(null, error)` |
| `Entity<TId>? other` | `entity.Equals(null)` |

`Mode=NullableReferenceTypes`: **17/17 passed**. Further behaviour proven in
`tests/UnitTests/Domain/NullGuardBehaviourTests.cs` (24/24 unit tests):

| Case | Behaviour |
| --- | --- |
| Non-nullable parameter, null passed | throws `ArgumentNullException` |
| Nullable parameter (`string?`, `T?`), null passed | accepted |
| `Task<T?>` / `ValueTask<T?>` returning null | accepted |
| `Task<T>` (non-nullable) returning null | throws `InvalidOperationException` |
| `ValueTask<T>` (non-nullable) returning null | **NOT checked (known gap)** |
| Nullable property set/get null | accepted |
| Nullable generic `T?` return | accepted |
| Non-nullable return value null | throws `InvalidOperationException` |

## Consequences

- `?` annotations are the single source of truth for nullability, at compile time
  and at runtime. No `[AllowNull]` duplication.
- Known gap: non-nullable `ValueTask<T>` results are not checked. Use `Task<T>`
  for non-nullable async results.
- EF Core materialization through Domain is proven by the integration test that
  loads entities from real SQL Server.
