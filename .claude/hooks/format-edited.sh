#!/usr/bin/env bash
# Claude Code PostToolUse hook (Edit|Write): formats the edited C# file.
#
# WHITESPACE ONLY: runs `dotnet format whitespace --folder` on that one file
# (about 1 s per edit). The full style and analyzer check (`dotnet format
# --verify-no-changes` on the solution, about 5 s even for one file) lives in
# the backend's scripts/verify, not here.
#
# The format runs in the backend folder: the nearest parent of the edited file
# (inside the project) that holds a *.slnx file. No folder name is written here
# (ADR 0019). Files outside such a folder are skipped.
#
# Input: the hook JSON on stdin; the edited path is tool_input.file_path
# (absolute, native separators). Never blocks an edit: always exits 0 and
# reports problems on stderr.
set -uo pipefail

project_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"

# Prints two lines: the backend root and the file path relative to it.
located="$(python3 -c '
import glob, json, os, sys
try:
    data = json.load(sys.stdin)
except ValueError:
    sys.exit(0)
path = (data.get("tool_input") or {}).get("file_path") or ""
project = os.path.abspath(sys.argv[1])
if not path.lower().endswith(".cs"):
    sys.exit(0)
full = os.path.abspath(path)
try:
    if os.path.relpath(full, project).startswith(".."):
        sys.exit(0)
except ValueError:  # different drive on Windows
    sys.exit(0)
directory = os.path.dirname(full)
while True:
    if glob.glob(os.path.join(glob.escape(directory), "*.slnx")):
        break
    if os.path.normcase(directory) == os.path.normcase(project):
        sys.exit(0)
    parent = os.path.dirname(directory)
    if parent == directory:
        sys.exit(0)
    directory = parent
print(directory)
print(os.path.relpath(full, directory).replace(os.sep, "/"))
' "$project_dir" 2>/dev/null)"

[ -n "$located" ] || exit 0

backend_root="$(printf '%s\n' "$located" | sed -n 1p)"
relative_path="$(printf '%s\n' "$located" | sed -n 2p)"
[ -n "$backend_root" ] && [ -n "$relative_path" ] || exit 0

if ! dotnet format whitespace "$backend_root" --folder --include "$relative_path" >/dev/null 2>&1; then
  echo "format hook: dotnet format whitespace failed for $relative_path (scripts/verify runs the full check)" >&2
fi

exit 0
