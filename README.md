# Smart Task Management System

A task management system built as a hiring assignment: an ASP.NET Core 10 API
(Clean Architecture, DDD, CQRS Lite) and an Angular frontend. Code quality,
security and documentation matter more than the number of features.

## Where things live

| Folder | Content |
| --- | --- |
| [`SmartTaskManagement-Backend/`](SmartTaskManagement-Backend/README.md) | The .NET API: source, tests, scripts, Docker files, backend docs and ADRs |
| [`SmartTaskManagement-Frontend/`](SmartTaskManagement-Frontend/README.md) | The Angular app (built in a later step) |
| [`docs/`](docs/) | Project-wide docs: [assignment](docs/assignment.md), [PRD](docs/PRD.md), [context](docs/CONTEXT.md) |
| `.github/workflows/` | CI (`backend.yml` runs the backend verify script) |
| `.claude/` | Claude Code rules, agents, settings and hooks |

The layout is recorded in
[ADR 0019](SmartTaskManagement-Backend/docs/adr/0019-repository-layout.md).

## Quick start (backend)

```bash
cd SmartTaskManagement-Backend
bash scripts/verify.sh
```

Setup, local run and API overview: the
[backend README](SmartTaskManagement-Backend/README.md).
