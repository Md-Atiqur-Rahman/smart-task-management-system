# ADR 0003: AutoMapper dropped; hand-written projections

- Status: Accepted
- Date: 2026-11-08

## Context

AutoMapper was planned at version 14.x for DTO projection (`ProjectTo`).
Checked on nuget.org on 2026-11-07:

- 14.0.0 is the only 14.x release. It is MIT-licensed but flagged with
  GHSA-rvv3-g6hj-g44x, "AutoMapper Vulnerable to Denial of Service (DoS) via
  Uncontrolled Recursion", severity high, affecting all versions below 15.1.1.
- The patched versions (15.1.1 and later, newest 16.3.0) are no longer MIT; they
  ship a commercial license file.
- The vulnerability gate is strict: NuGetAudit fails the build and
  `scripts/check-packages` fails verify on any vulnerable package.

## Decision

- No AutoMapper. Read repositories project with hand-written `Select`
  expressions (for example `SampleReadRepository.ToDto`).
- The gate stays strict; no suppression of the advisory.
- The proof that the gate catches it: a scratch project with AutoMapper 14.0.0
  makes `scripts/check-packages.sh` fail with
  `AutoMapper 14.0.0 (topLevelPackages)`.

## Consequences

- A few lines of projection code per DTO, checked by the compiler and
  translated by EF Core to SQL; no runtime mapping configuration.
- No commercial license and no vulnerable dependency.
