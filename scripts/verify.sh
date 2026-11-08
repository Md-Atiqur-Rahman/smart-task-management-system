#!/usr/bin/env bash
# Single verify entry point. CI runs only this script; verify.ps1 runs the same
# checks on Windows. Exits non-zero on the first failure.
# Requires: .NET SDK (global.json), git, python3, Docker.
set -euo pipefail

cd "$(dirname "$0")/.."

fail() {
  echo "VERIFY FAILED: $*" >&2
  exit 1
}

step() {
  echo
  echo "==> $*"
}

# Fails when `git grep` finds a match (exit 0) AND when git grep itself errors
# (exit > 1); only exit 1 (no match) passes. Usage: forbid "<message>" <git grep args>
forbid() {
  local message="$1"; shift
  local output rc=0
  output="$(git grep "$@")" || rc=$?
  if [ "$rc" -eq 0 ]; then
    printf '%s
' "$output"
    fail "$message"
  elif [ "$rc" -gt 1 ]; then
    fail "git grep failed (exit $rc) while checking: $message"
  fi
}

env_value() {
  grep -E "^$1=" .env.example | head -n 1 | cut -d= -f2-
}

# The solution is the single *.slnx file in the repository root (no name literal here).
mapfile -t solutions < <(find . -maxdepth 1 -name '*.slnx' -printf '%f\n')
[ "${#solutions[@]}" -eq 1 ] || fail "expected exactly one *.slnx in the repository root, found ${#solutions[@]}"
SOLUTION="${solutions[0]}"

# 1. Restore and build (Release, 0 warnings; NuGetAudit also fails restore).
step "1. Restore and build (Release, 0 warnings)"
dotnet restore "$SOLUTION" || fail "restore failed"
build_log="$(mktemp)"
dotnet build "$SOLUTION" -c Release --no-restore -tl:off | tee "$build_log" || fail "build failed"
grep -Eq "^ +0 Warning\(s\)" "$build_log" || fail "build produced warnings"
grep -Eq "^ +0 Error\(s\)" "$build_log" || fail "build produced errors"

# 2. Formatting (dotnet format is the only formatter).
step "2. dotnet format --verify-no-changes"
dotnet format "$SOLUTION" --verify-no-changes || fail "formatting differs; run: dotnet format"

# 3. Tests (unit, architecture, integration + smoke against SQL Server via Testcontainers).
step "3. dotnet test"
# TRX reports go to TestResults/ (git-ignored); CI uploads them as an artifact.
rm -rf TestResults
dotnet test --solution "$SOLUTION" -c Release --no-build   --report-xunit-trx --results-directory "$PWD/TestResults" || fail "tests failed"

# 4. Vulnerable and deprecated packages (parsed; the command itself exits 0).
step "4. Vulnerable and deprecated packages"
bash scripts/check-packages.sh "$SOLUTION" || fail "vulnerable or deprecated packages"

# 5. Placeholder check. These files name the placeholder on purpose.
step "5. Placeholder check (MyApp)"
forbid "placeholder MyApp found (see lines above)" --untracked -n -i "MyApp" -- . ':!scripts/verify.sh' ':!scripts/verify.ps1'
echo "    ok"

# 6. Naming consistency and TLS rule. The names are read from Directory.Build.props
# and .env.example; this script contains no project or company name.
step "6. Naming checks and TLS check"
project_name="$(env_value PROJECT_NAME)"
image_name="$(env_value IMAGE_NAME)"
db_name="$(env_value DB_NAME)"
app_assembly="$(env_value APP_ASSEMBLY)"
props_project="$(sed -n 's:.*<PROJECT_NAME>\(.*\)</PROJECT_NAME>.*:\1:p' Directory.Build.props)"
props_company="$(sed -n 's:.*<COMPANY_NAME>\(.*\)</COMPANY_NAME>.*:\1:p' Directory.Build.props)"
lower_project="$(printf '%s' "$project_name" | tr '[:upper:]' '[:lower:]')"
lower_company="$(printf '%s' "$props_company" | tr '[:upper:]' '[:lower:]')"

[ -n "$props_company" ] || fail "COMPANY_NAME missing in Directory.Build.props"
[ -n "$project_name" ] || fail "PROJECT_NAME missing in .env.example"
[ "$image_name" = "$lower_project" ] || fail "IMAGE_NAME ($image_name) must equal lowercase PROJECT_NAME ($lower_project)"
[ "$db_name" = "$project_name" ] || fail "DB_NAME ($db_name) must equal PROJECT_NAME ($project_name)"
[ "$project_name" = "$props_project" ] || fail "PROJECT_NAME differs: .env.example=$project_name Directory.Build.props=$props_project"
[ "$app_assembly" = "$props_company.$props_project.Api" ] || fail "APP_ASSEMBLY ($app_assembly) must be $props_company.$props_project.Api"
# PascalCase and lowercase forms of both names (case-insensitive, fixed strings).
forbid "project or company name ($props_company/$props_project) found in compose/Dockerfile/workflows/appsettings (see above)" \
  --untracked -n -i -F -e "$props_company" -e "$lower_company" -e "$props_project" -e "$lower_project" -- \
  docker-compose.yml Dockerfile '.github/workflows/*.yml' '**/appsettings*.json'
forbid "TrustServerCertificate found (see lines above). Use Encrypt=false only in Development/Testing (ADR 0011)." \
  --untracked -n -i "TrustServerCertificate" -- . ':!docs/' ':!scripts/verify.sh' ':!scripts/verify.ps1'
echo "    ok"

# 7. OpenAPI: deterministic and identical to the committed docs/openapi.json.
step "7. OpenAPI: generate twice, compare hashes and the committed docs/openapi.json"
bash scripts/generate-openapi.sh || fail "OpenAPI generation failed (run 1)"
first="$(sha256sum docs/openapi.json | cut -d' ' -f1)"
bash scripts/generate-openapi.sh || fail "OpenAPI generation failed (run 2)"
second="$(sha256sum docs/openapi.json | cut -d' ' -f1)"
echo "    run 1: $first"
echo "    run 2: $second"
[ "$first" = "$second" ] || fail "OpenAPI generation is not deterministic"
git ls-files --error-unmatch docs/openapi.json >/dev/null 2>&1 \
  || fail "docs/openapi.json is not committed. Regenerate: bash scripts/generate-openapi.sh"
git diff --quiet -- docs/openapi.json \
  || fail "docs/openapi.json is out of date. Regenerate (bash scripts/generate-openapi.sh) and commit it."
echo "    ok"

# 8. Docker image builds (no push).
step "8. docker build (no push)"
docker build --build-arg APP_ASSEMBLY="$app_assembly" -t "$image_name:verify" . || fail "docker build failed"

echo
echo "VERIFY PASSED"
