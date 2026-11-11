#!/usr/bin/env bash
# Fails when any package (top-level or transitive) is vulnerable or deprecated.
# Usage: bash scripts/check-packages.sh [project-or-solution]
#
# `dotnet package list --vulnerable` exits 0 even when it finds something
# (NuGet/Home#11315), so the JSON output is parsed and the script fails explicitly.
set -euo pipefail

# Default: the single *.slnx in the backend root, as an absolute path so the
# script works from any directory.
target="${1:-$(cd "$(dirname "$0")/.." && find "$PWD" -maxdepth 1 -name '*.slnx' | head -n 1)}"

read -r -d '' PARSER <<'PY' || true
import json, sys
kind = sys.argv[1]
report = json.load(sys.stdin)
findings = []
for problem in report.get('problems', []):
    findings.append('problem: ' + str(problem.get('text', problem)))
for project in report.get('projects', []):
    path = project.get('path', '?')
    for problem in project.get('problems', []):
        findings.append(path + ': problem: ' + str(problem.get('text', problem)))
    for framework in project.get('frameworks', []):
        for group in ('topLevelPackages', 'transitivePackages'):
            for package in framework.get(group, []):
                version = package.get('resolvedVersion', package.get('requestedVersion', '?'))
                findings.append(path + ': ' + package['id'] + ' ' + version + ' (' + group + ')')
if findings:
    print(kind + ' packages found:')
    for finding in findings:
        print('  ' + finding)
    sys.exit(1)
print('    no ' + kind + ' packages')
PY

check() {
  local kind="$1"; shift
  local json rc=0
  # Capture the exit code explicitly: under `set -e` a failing command
  # substitution would otherwise end the script without any message.
  json="$(dotnet package list --project "$target" "$@" --format json 2>&1)" || rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "check-packages: 'dotnet package list $*' failed (exit $rc):" >&2
    printf '%s\n' "$json" >&2
    exit "$rc"
  fi
  printf '%s' "$json" | python3 -c "$PARSER" "$kind"
}

check vulnerable --vulnerable --include-transitive
check deprecated --deprecated --include-transitive
