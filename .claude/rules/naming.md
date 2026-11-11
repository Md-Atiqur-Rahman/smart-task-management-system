# Naming rules

Paths in this file are relative to the backend folder `<PROJECT_NAME>-Backend/`
(ADR 0019).

- Names derive from two inputs in `Directory.Build.props`: `COMPANY_NAME` and
  `PROJECT_NAME`. Assembly and root namespace: `<COMPANY_NAME>.<PROJECT_NAME>.<Folder>`.
  Never hardcode either name anywhere else.
- Top-level folders (repository root, ADR 0019): `<PROJECT_NAME>-Backend/` and
  `<PROJECT_NAME>-Frontend/`, each with its own README, docs, compose and env
  files. The root keeps only `.github/`, `.claude/`, git files, a short
  `README.md` and `CLAUDE.md`, and the project-wide `docs/` (assignment, PRD,
  CONTEXT). Tooling (workflows, scripts, the format hook) derives the folders
  (`*-Backend`, the folder holding the `*.slnx`) and never writes the project
  name; renaming `PROJECT_NAME` means renaming both folders by hand.
- Folders and projects inside the backend: `src/{Domain,Application,Infrastructure,Api}`,
  `tests/{UnitTests,IntegrationTests,ArchitectureTests,WeavingProbe}`; csproj
  name = folder name.
- `.env.example` is the single source for Docker names: `IMAGE_NAME` = lowercase
  `PROJECT_NAME`, `DB_NAME` = `PROJECT_NAME`, `APP_ASSEMBLY` =
  `<COMPANY_NAME>.<PROJECT_NAME>.Api`. The company name never appears in image,
  container or database names. `docker-compose.yml`, the `Dockerfile` (backend
  root), the workflows (`../.github/workflows/*.yml`) and `appsettings*.json`
  contain neither name (verify checks it).
- Use cases: `<Verb><Noun>Command|Query`, `<...>CommandHandler|QueryHandler`,
  `<...>Validator`; one folder per use case.
- Read side: `I<Name>ReadRepository` / `<Name>ReadRepository`, DTOs `<Name>Dto`.
- Write side: `I<Aggregate>Repository` / `<Aggregate>Repository`.
- Errors: `<module>.<snake_case_reason>`; aggregate errors in `<Aggregate>Errors`.
- Options classes: `<Concern>Options` with a `SectionName` constant.
- Composition: `Configure<Concern>` (Application/Infrastructure),
  `Add<Concern>` / `Use<Concern>` (Api).
- Tests: `Method_Condition_ExpectedResult` (CA1707 is off for test projects only).
- No placeholder or template names anywhere; scripts/verify searches the whole
  repository (root, backend and frontend) for the placeholder application name
  and fails if it appears.
