# CLAUDE.md

Smart Task Management System: a .NET 10 Clean Architecture API and an Angular
frontend in one repository. This file holds the project-wide rules; each part
has its own `CLAUDE.md` with its strict rules.

## 1. No-Hallucination Policy (non-negotiable, applies to every step)

- Never guess a package name, version, API, config key, CLI flag or file syntax.
- Verify every package and version (`dotnet package search <id> --exact-match`,
  the npm registry or official docs) BEFORE adding it. Never write versions from
  memory.
- If unsure, write "UNVERIFIED: <what>" and ask. Do not invent.
- Mark every assumption as "ASSUMPTION:" in reports.
- Never say "done" or "pass" without running the real command and showing its output.
- Report clearly what was NOT done or could NOT be verified.

## 2. Source of truth (in this order)

1. `docs/assignment.md`: the brief.
2. `docs/PRD.md`: the decisions the brief leaves open (one PRD; changes go into
   its change log).
3. ADRs: `SmartTaskManagement-Backend/docs/adr/` (an accepted decision changes
   only through a new ADR).
4. Design documents and rules: each part's `docs/`, `CLAUDE.md` and
   `.claude/rules/`.
5. `docs/CONTEXT.md`: current state, working rules and next steps.

If two sources conflict, ask the developer; do not choose silently.

## 3. Where each part lives (ADR 0019)

| Path | Content |
| --- | --- |
| `SmartTaskManagement-Backend/` | .NET API; rules in `SmartTaskManagement-Backend/CLAUDE.md`; run its commands from that folder |
| `SmartTaskManagement-Frontend/` | Angular app; its `CLAUDE.md` is added with the frontend |
| `docs/` | Project-wide docs: `assignment.md`, `PRD.md`, `CONTEXT.md` |
| `.claude/` | Rules (paths relative to the backend folder), agents, settings, format hook |
| `.github/workflows/` | CI; `backend.yml` runs the backend verify script |

Tooling (workflows, scripts, hooks) never writes the project name; it finds
the backend as the `*-Backend` folder or the folder that holds the `*.slnx`.

## 4. Commits

Prefixes: `feat:`, `fix:`, `docs:`. One logical change per commit. Never push
or merge without the developer's go-ahead.

## 5. Verify

Backend full check (also the CI step): `bash SmartTaskManagement-Backend/scripts/verify.sh`
(or `cd SmartTaskManagement-Backend && bash scripts/verify.sh`). Details in
`SmartTaskManagement-Backend/CLAUDE.md`.
