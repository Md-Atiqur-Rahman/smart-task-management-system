# ADR 0019: Repository layout with separate backend and frontend folders

- Status: Accepted
- Date: 2026-11-11

## Context

The repository started as a backend-only template: the solution, `src/`,
`tests/`, `scripts/`, `docs/`, the Dockerfile, `docker-compose.yml` and
`.env.example` all lived in the repository root. An Angular frontend will be
added in the same repository (PRD section 11). The developer wants the backend
and the frontend in separate folders, each with its own README, docs, compose
and env files, so that one part can be read, built and changed without the
other.

Constraints:

- GitHub reads workflows only from `.github/workflows/` in the repository root.
- Claude Code reads `.claude/` and `CLAUDE.md` from the project root.
- The naming rule (`.claude/rules/naming.md`) forbids the project and company
  names in `docker-compose.yml`, the `Dockerfile`, the workflows and
  `appsettings*.json`; `scripts/verify` checks it.

## Decision

- Two top-level folders: `<PROJECT_NAME>-Backend/` and `<PROJECT_NAME>-Frontend/`
  (today `SmartTaskManagement-Backend/` and `SmartTaskManagement-Frontend/`).
- The backend folder holds everything the backend needs: the solution, `src/`,
  `tests/`, `scripts/`, the build and package settings (`Directory.*`,
  `global.json`, `nuget.config`, `dotnet-tools.json`, `FodyWeavers.xml`,
  `.editorconfig`), the `Dockerfile`, `.dockerignore`, `docker-compose.yml`,
  `.env.example` (and the local, git-ignored `.env`), its README and CLAUDE.md,
  and its `docs/` (architecture, coding standards, sample flow, design
  documents, ADRs, plans, `openapi.json`).
- The root keeps only CI (`.github/`), Claude Code tooling (`.claude/`), the git
  files, a short `README.md` and `CLAUDE.md`, and the project-wide docs in
  `docs/`: `assignment.md`, `PRD.md` and `CONTEXT.md`.
- Tooling derives the folders and never writes the project name:
  - the backend workflow (`.github/workflows/backend.yml`) finds exactly one
    `*-Backend` folder in a shell step and runs `scripts/verify.sh` there; its
    `paths` filter (`*-Backend/**`, `docs/**`, the workflow file) uses GitHub's
    glob syntax, where `*` does not match `/`;
  - the scripts change to their own backend root (the parent of `scripts/`), so
    they work from any directory; the verify placeholder check covers the whole
    repository and the name check covers the root workflows;
  - the format hook formats an edited `.cs` file in its nearest parent folder
    that holds a `*.slnx`.
- Paths in the backend's docs and in `.claude/rules/` are relative to the
  backend folder; backend docs refer to the root docs as `../docs/...`.

## Consequences

- Renaming `PROJECT_NAME` means renaming both top-level folders by hand, and the
  backend script paths in `.claude/settings.json`; `scripts/new-project` renames
  only inside the backend folder.
- CI runs per part: the backend workflow runs only when the backend, the root
  docs or the workflow itself changes. The frontend gets its own workflow later.
  A skipped workflow leaves its checks pending, so a branch protection rule that
  requires the backend check would block a frontend-only pull request.
- Commands run from the backend folder (`cd <PROJECT_NAME>-Backend`);
  `scripts/verify.sh` also runs from anywhere.
- Every file moved with `git mv`, so `git log --follow` keeps its history.
