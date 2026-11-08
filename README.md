# Datavanced.SmartTaskManagement

ASP.NET Core (.NET 10) Clean Architecture boilerplate with one runnable Sample feature.
Every rule is enforced by code: build settings, architecture tests and one verify
script (`scripts/verify.sh`), which is also the only CI step.

## What is inside

- **Layers:** `src/Domain`, `src/Application`, `src/Infrastructure`, `src/Api`
  (dependency rule enforced by architecture tests).
- **Patterns:** DDD building blocks, CQRS Lite with own handler interfaces and a
  Logging -> Validation pipeline, write repositories + `IUnitOfWork`, read
  repositories returning DTOs, `Result` for expected failures, ProblemDetails with
  `errorCode` for every error.
- **Persistence:** EF Core + SQL Server, soft delete, audit fields from
  `TimeProvider`, `RowVersion` concurrency (stale writes return 409).
- **Security baseline:** JWT bearer skeleton, security context set once by
  middleware, HTTPS/HSTS, security headers, CORS and rate limiting from validated
  Options, secrets only in user-secrets or environment variables.
- **Quality gates:** warnings as errors with all analyzers, NullGuard runtime null
  checks, `dotnet format`, vulnerable/deprecated package check, committed
  OpenAPI document checked for freshness, Docker image build.
- **Sample feature:** five endpoints under `/api/samples`, the copy-source for new
  features ([docs/sample-flow.md](docs/sample-flow.md)).

## Repository layout

| Path | Content |
| --- | --- |
| `src/` | The four layers |
| `tests/UnitTests` | Domain and Application tests |
| `tests/IntegrationTests` | Real host + SQL Server (Testcontainers), smoke test |
| `tests/ArchitectureTests` | Layer, naming, DI-style and API-surface rules |
| `tests/WeavingProbe` | Woven helper library for the NullGuard behaviour tests |
| `scripts/` | `verify`, `generate-openapi`, `check-packages`, `new-project` (`.sh` and `.ps1`) |
| `docs/` | Architecture, coding standards, ADRs, Sample flow, `openapi.json` |
| `.claude/` | Claude Code rules, agents and settings |

## Requirements

- .NET SDK 10.0.401 (pinned in `global.json`)
- Docker (SQL Server for local runs and integration tests)

## Run locally

1. `cp .env.example .env`
2. `docker compose up -d db`
3. Copy the secrets into user-secrets once (Git Bash, repo root):

   ```bash
   set -a; . ./.env; set +a; for kv in "Database:Name=$DB_NAME" "Database:Password=$DB_PASSWORD" "Jwt:SigningKey=$JWT_SIGNING_KEY"; do dotnet user-secrets set "${kv%%=*}" "${kv#*=}" --project src/Api; done
   ```

   PowerShell:

   ```powershell
   $e=@{}; Get-Content .env | ? { $_ -match '^\w+=' } | % { $k,$v = $_ -split '=',2; $e[$k]=$v }; dotnet user-secrets set Database:Name $e.DB_NAME --project src/Api; dotnet user-secrets set Database:Password $e.DB_PASSWORD --project src/Api; dotnet user-secrets set Jwt:SigningKey $e.JWT_SIGNING_KEY --project src/Api
   ```

   Environment-variable equivalents: `Database__Name`, `Database__Password`, `Jwt__SigningKey`.
4. `dotnet run --project src/Api --launch-profile http`
5. Swagger UI (Development only): `http://localhost:5199/swagger`

Full walkthrough and request paths: [docs/sample-flow.md](docs/sample-flow.md).

## Run the full stack in Docker (local only)

```bash
cp .env.example .env
docker compose up -d --build
curl http://127.0.0.1:5080/health
docker compose down
```

> **`docker-compose.yml` is for local development only, not for production.**
> The api container runs with `ASPNETCORE_ENVIRONMENT=Development` because the
> local SQL Server has no trusted certificate and `Database:Encrypt=false` is
> rejected in every other environment (ADR 0011). All ports bind to `127.0.0.1`,
> and the values come from the dev-only placeholders in `.env`.

## Roadmap

- **Phase 2: authentication.** Register, login and refresh endpoints, a `User`
  aggregate, an admin seed, `[Authorize]` on every endpoint, and the Bearer
  requirement per protected operation in the OpenAPI document (checklist in
  `docs/sample-flow.md`).
- **Phase 1.5: tooling.** A `/scaffold` Claude Code skill that generates a new
  feature slice from Sample, and a matching Angular front-end boilerplate.
- **Open decisions.**
  - Docker image tag strategy for local and CI builds (today: `<image>:local` and
    `<image>:verify`; no registry push).
  - Whether to enforce the 80-character line guideline with a built-in analyzer
    rule, if one exists (today it is a guideline only; `dotnet format` does not
    wrap lines).
  - Reverse-proxy support: forwarded headers with explicit trusted proxies (see
    Known limitations).

## Known limitations

- **Reverse proxies are not configured.** There is no forwarded-headers handling
  (`X-Forwarded-For` / `X-Forwarded-Proto`). Behind a reverse proxy or load
  balancer:
  - **rate limiting sees the proxy's IP address**, not the client's, so every
    client shares one limit;
  - **HTTPS redirection may loop**: a TLS-terminating proxy forwards plain
    `http`, the app redirects to `https`, and the proxy sends `http` again.
  **Before production, configure forwarded headers with explicit trusted proxies**
  (known proxy addresses or networks, from validated Options; never trust all
  sources). `/health` is already excluded from rate limiting so probes never
  compete with client traffic.

## Create a project from this template

1. Copy the repository (a fresh clone or a commit with a clean working tree),
   then rename it with the script. Names are PascalCase, letters and digits only.
   The script replaces the current names (from `Directory.Build.props`) in file
   contents, file and folder names, generates a new `UserSecretsId`, regenerates
   `docs/openapi.json` and prints every change. It refuses to run with uncommitted
   changes or when a new name already occurs in the files.

   Git Bash:

   ```bash
   bash scripts/new-project.sh YourCompany YourProject
   bash scripts/verify.sh
   ```

   Windows PowerShell 5.1:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/new-project.ps1 YourCompany YourProject
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
   ```

   > **Windows note:** in PowerShell or cmd, plain `bash` may resolve to the WSL
   > launcher (`C:\Users\<you>\AppData\Local\Microsoft\WindowsApps\bash.exe`)
   > instead of Git Bash, and fail or run in a different environment. Run the
   > `.sh` scripts from a Git Bash terminal, call Git Bash explicitly
   > (`& "C:\Program Files\Git\bin\bash.exe" scripts/verify.sh`), or use the
   > `.ps1` scripts.

   Then commit the result and set the user-secrets for the new project (see
   "Run locally").
2. Replace the dev-only placeholders in `.env.example`; real secrets go only into
   user-secrets or environment variables.
3. Add features by copying the Sample slice, then remove Sample
   (checklists in `docs/sample-flow.md`).
4. Add authentication (Phase 2 checklist in `docs/sample-flow.md`).
5. **If the API will run behind a reverse proxy or load balancer, configure
   forwarded headers with explicit trusted proxies first** (see Known
   limitations): otherwise rate limiting sees only the proxy's IP and HTTPS
   redirection may loop.
6. Production database: `Database:Encrypt=true` with a certificate the client
   trusts (enforced outside Development and Testing).
7. Run `bash scripts/verify.sh` and push; CI runs the same script.

## Tests and checks

- `dotnet test --solution Datavanced.SmartTaskManagement.slnx`
- Regenerate the OpenAPI document: `bash scripts/generate-openapi.sh`
- Verify (Git Bash, Linux, CI): `bash scripts/verify.sh`
- Verify (Windows): `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`

> **Tested PowerShell:** the `.ps1` scripts were run and passed with
> **Windows PowerShell 5.1** (5.1.26100). They were **not** tested with
> PowerShell 7 (`pwsh`), which is not installed on the development machine.
> CI runs only `scripts/verify.sh`.

## Documentation

- [docs/architecture.md](docs/architecture.md), [docs/adr/](docs/adr/README.md)
- [docs/coding-standards.md](docs/coding-standards.md), [docs/sample-flow.md](docs/sample-flow.md)

## Contributing

Use the commit prefixes `feat:`, `fix:` and `docs:`, run the verify script before
pushing, and follow the pull request checklist (`.github/pull_request_template.md`).
