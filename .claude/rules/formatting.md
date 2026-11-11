# Formatting rules

Paths in this file are relative to the backend folder `<PROJECT_NAME>-Backend/`
(ADR 0019).

- `dotnet format` is the only formatter; `.editorconfig` is the single source
  (LF, UTF-8, final newline). No other formatter (no CSharpier).
- The PostToolUse hook (`../.claude/hooks/format-edited.sh`, wired in
  `../.claude/settings.json`, both at the repository root) runs after every
  Edit/Write of a `.cs` file. It fixes **whitespace only** (`dotnet format
  whitespace --folder --include <file>`, run in the backend folder found as the
  nearest parent holding a `*.slnx`; about 1 s per edit). It does not apply
  code-style or analyzer fixes.
- The full style and analyzer check lives in `scripts/verify`
  (`dotnet format --verify-no-changes` on the solution, plus the Release build
  with warnings as errors). A green hook does not mean the code is formatted to
  the full rules; run verify.
- The hook never blocks an edit: on failure it prints a message on stderr and
  exits 0.
- Hooks run in Git Bash on Windows (`"shell": "bash"` in settings), so the hook
  is a `.sh` script.
