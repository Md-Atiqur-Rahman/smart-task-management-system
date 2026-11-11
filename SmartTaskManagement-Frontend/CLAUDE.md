# CLAUDE.md (frontend)

Angular frontend of the Smart Task Management System. Read first: the root
`../CLAUDE.md` (No-Hallucination Policy, commits), `docs/frontend-design.md`
(the design), `docs/plans/` (the current plan). The API contract is
`../SmartTaskManagement-Backend/docs/openapi.json`; the API is the authority.

## Commands (run in this folder)

| Task                                                       | Command                                  |
| ---------------------------------------------------------- | ---------------------------------------- |
| Install                                                    | `npm ci`                                 |
| Dev server (HTTPS, https://localhost:4200)                 | `npm start` (needs `certs/`, see README) |
| Full check (lint, format, type-check, tests, build, audit) | `bash scripts/verify.sh`                 |
| Unit tests                                                 | `npm run test:ci`                        |
| Format                                                     | `npm run format`                         |

## Architecture (Clean Architecture for Angular)

- `src/app/core/`: app-wide singletons, no UI pages: `auth/` (session store,
  auth API, interceptors, guards), `http/` (error mapping), `config/`
  (API base URL token), `realtime/` (SSE), `notifications/`, `layout/` (shell).
- `src/app/shared/`: reusable UI and pure helpers with no feature knowledge.
- `src/app/features/<feature>/`: `pages/` (routed, lazy-loaded),
  `components/`, `data-access/` (one service per API resource; the only place
  that calls `HttpClient`), `models/` (types named like the `openapi.json`
  schemas).
- Dependency direction: features -> shared -> core; core never imports a
  feature; features never import each other's pages or components (only
  data-access services when needed).

## Rules

- Standalone components, `ChangeDetectionStrategy.OnPush` (lint error
  otherwise), signals for state, `inject()`, typed reactive forms, strict
  TypeScript and `strictTemplates`, no `any` (lint error).
- Every feature route is lazy (`loadComponent` / `loadChildren`) and guarded
  (`authGuard`, `roleGuard`); guards are UX only, the API enforces the rules.
- HTTP concerns live in interceptors only: bearer token (API host only),
  single-flight refresh on 401, ProblemDetails to `ApiProblem`.
- Access token in memory only; never `localStorage`, `sessionStorage` or a URL.
  `withCredentials: true` only on `POST /api/auth/register|login|refresh|logout`.
- Call only endpoints and fields from `openapi.json`; enums are the API strings.
  No backend rule duplicated beyond UX hints (form limits, which buttons show).
  Any task status may change to any status.
- Packages: verify with `npm view <pkg> version` and its license before adding;
  exact versions (`.npmrc` `save-exact=true`); commit `package-lock.json`;
  `npm audit --audit-level=high` must pass.
- Never commit `certs/` (exported dev certificate) or any secret.
- Accessibility: real buttons, links and labels, visible focus, `aria-label` on
  icon buttons, 4.5:1 contrast, works from 360 px wide.
- Tests: Vitest (`*.spec.ts` next to the code); write the failing test first.
