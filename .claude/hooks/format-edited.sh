#!/usr/bin/env bash
# Claude Code PostToolUse hook (Edit|Write): formats the edited C# file.
#
# WHITESPACE ONLY: runs `dotnet format whitespace --folder` on that one file
# (about 1 s per edit). The full style and analyzer check (`dotnet format
# --verify-no-changes` on the solution, about 5 s even for one file) lives in
# scripts/verify, not here.
#
# Input: the hook JSON on stdin; the edited path is tool_input.file_path
# (absolute, native separators). Never blocks an edit: always exits 0 and
# reports problems on stderr.
set -uo pipefail

project_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"

relative_path="$(python3 -c '
import json, os, sys
try:
    data = json.load(sys.stdin)
except ValueError:
    sys.exit(0)
path = (data.get("tool_input") or {}).get("file_path") or ""
root = os.path.abspath(sys.argv[1])
if not path.lower().endswith(".cs"):
    sys.exit(0)
full = os.path.abspath(path)
try:
    rel = os.path.relpath(full, root)
except ValueError:  # different drive on Windows
    sys.exit(0)
if rel.startswith(".."):
    sys.exit(0)
print(rel.replace(os.sep, "/"))
' "$project_dir" 2>/dev/null)"

[ -n "$relative_path" ] || exit 0

if ! dotnet format whitespace "$project_dir" --folder --include "$relative_path" >/dev/null 2>&1; then
  echo "format hook: dotnet format whitespace failed for $relative_path (scripts/verify runs the full check)" >&2
fi

exit 0
