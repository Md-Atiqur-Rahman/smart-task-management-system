# NullGuard rules

Decision and evidence: `docs/adr/0008-nullguard.md`.

- Configuration lives ONLY in the root `FodyWeavers.xml`
  (`Mode="NullableReferenceTypes"`) and `src/SharedAssemblyInfo.cs`
  (`ValidationFlags.Arguments | ValidationFlags.ReturnValues`). Never add a
  per-project `FodyWeavers.xml`; `Directory.Build.targets` feeds the root file
  through `WeaverConfiguration`.
- The `?` annotation decides what may be null, at compile time and at runtime:
  - non-nullable parameter + null -> `ArgumentNullException`
  - non-nullable return value or `Task<T>` result + null -> `InvalidOperationException`
  - `T?`, `string?`, `Task<T?>`, `ValueTask<T?>` -> null is accepted
- Write `?` honestly. Do not use `null!` to silence the compiler in src code;
  it becomes a runtime exception.
- Known gap: non-nullable `ValueTask<T>` results are NOT checked. Use `Task<T>`
  for non-nullable async results. Enforced by the architecture test
  `NullGuardGapTests.Src_HasNoNonNullableReferenceValueTask` (reference-type `T`
  without `?` fails; `ValueTask<T?>` and value-type results are allowed;
  overrides of framework members are exempt because the framework fixes their
  signature, e.g. `PartitionedRateLimiter.AcquireAsyncCore`).
- Properties and non-public members are not checked (EF Core materializes
  entities through private constructors and setters).
- Test projects (`IsTestProject=true`) are not woven. `tests/WeavingProbe` is
  woven on purpose and holds the probe members for the behaviour tests.
- Changing the mode or flags needs a new ADR and a green
  `NullGuardBehaviourTests` run.
