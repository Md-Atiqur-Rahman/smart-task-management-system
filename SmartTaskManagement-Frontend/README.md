# SmartTaskManagement-Frontend

Angular 22 frontend of the Smart Task Management System: login and register,
role-aware dashboard, project and task management with search, filters, sorting
and paging, and live notifications. Design: `docs/frontend-design.md`; screens:
`docs/design/prototype/`; plan: `docs/plans/`.

## Stack

Angular 22.2 (standalone components, signals, zoneless, lazy routes, guards,
interceptors, typed reactive forms), TypeScript 6.0 (strict, strictTemplates),
Vitest, angular-eslint, Prettier. Node.js `^22.22.3 || ^24.15.0 || >=26` (`.nvmrc`).

## Run locally

1. Start the backend (see `../SmartTaskManagement-Backend/README.md`): Docker
   Desktop running, `docker compose up -d` in the backend folder, then
   `dotnet run --project src/Api --launch-profile https` (API on
   `https://localhost:7254`).
2. Allow the frontend origin in the API (user-secret, no tracked file changes),
   in `SmartTaskManagement-Backend/src/Api`:

   ```bash
   dotnet user-secrets set "Cors:AllowedOrigins:0" "https://localhost:4200"
   ```

3. Export the trusted ASP.NET Core development certificate for the dev server
   (once; `certs/` is git-ignored). In this folder:

   ```bash
   dotnet dev-certs https --trust
   dotnet dev-certs https --export-path certs/localhost.pem --format Pem --no-password
   ```

   This writes `certs/localhost.pem` and `certs/localhost.key`.

4. `npm ci`, then `npm start` and open `https://localhost:4200`.

Why HTTPS in development: the refresh token is a `SameSite=Strict` cookie set by
`https://localhost:7254`; a page on `http://localhost:4200` is cross-site to it
(different scheme), so the browser would not send the cookie.

## Checks

`bash scripts/verify.sh` runs lint, Prettier check, type-check, unit tests, a
production build and `npm audit --audit-level=high`.

## Structure

```text
src/app/
  core/       auth (session, interceptors, guards), http errors, config, realtime, layout
  shared/     reusable UI and helpers
  features/   auth, dashboard, projects, tasks, users, notifications
              each: pages/ components/ data-access/ models/
src/environments/  API base URL only (no secrets)
scripts/verify.sh  full check
```
