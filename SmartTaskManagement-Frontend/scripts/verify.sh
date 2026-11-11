#!/usr/bin/env bash
# Full frontend check: lint, format, type-check, unit tests, production build, audit.
# Run from anywhere: bash SmartTaskManagement-Frontend/scripts/verify.sh
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

step() { printf '\n==> %s\n' "$1"; }

step "1. Lint (angular-eslint)"
npm run lint --silent

step "2. Format check (Prettier)"
npm run format:check --silent

step "3. Type-check (strict, app and specs)"
npm run typecheck --silent

step "4. Unit tests (Vitest)"
npm run test:ci --silent

step "5. Production build"
npm run build --silent

step "6. Audit (fails on high or critical)"
npm audit --audit-level=high

printf '\nverify: all checks passed\n'
