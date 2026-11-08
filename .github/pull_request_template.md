## What and why

<!-- One or two sentences: what changes and why. -->

## Checklist

- [ ] Commit messages use `feat:`, `fix:` or `docs:`
- [ ] `bash scripts/verify.sh` passes locally (or `powershell -File scripts/verify.ps1` on Windows)
- [ ] New behaviour has tests (unit, integration, or architecture)
- [ ] Controllers or DTOs changed: `docs/openapi.json` regenerated (`bash scripts/generate-openapi.sh`) and committed
- [ ] Model changed: migration added (see `docs/sample-flow.md`)
- [ ] No secrets, connection strings or real credentials in the diff
- [ ] New analyzer suppressions are scoped and carry a reason
- [ ] Decision changed: ADR added or superseded in `docs/adr/`
