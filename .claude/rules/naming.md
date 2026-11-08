# Naming rules

- Names derive from two inputs in `Directory.Build.props`: `COMPANY_NAME` and
  `PROJECT_NAME`. Assembly and root namespace: `<COMPANY_NAME>.<PROJECT_NAME>.<Folder>`.
  Never hardcode either name anywhere else.
- Folders and projects: `src/{Domain,Application,Infrastructure,Api}`,
  `tests/{UnitTests,IntegrationTests,ArchitectureTests,WeavingProbe}`; csproj
  name = folder name.
- `.env.example` is the single source for Docker names: `IMAGE_NAME` = lowercase
  `PROJECT_NAME`, `DB_NAME` = `PROJECT_NAME`, `APP_ASSEMBLY` =
  `<COMPANY_NAME>.<PROJECT_NAME>.Api`. The company name never appears in image,
  container or database names. `docker-compose.yml`, the `Dockerfile`, workflows
  and `appsettings*.json` contain neither name (verify checks it).
- Use cases: `<Verb><Noun>Command|Query`, `<...>CommandHandler|QueryHandler`,
  `<...>Validator`; one folder per use case.
- Read side: `I<Name>ReadRepository` / `<Name>ReadRepository`, DTOs `<Name>Dto`.
- Write side: `I<Aggregate>Repository` / `<Aggregate>Repository`.
- Errors: `<module>.<snake_case_reason>`; aggregate errors in `<Aggregate>Errors`.
- Options classes: `<Concern>Options` with a `SectionName` constant.
- Composition: `Configure<Concern>` (Application/Infrastructure),
  `Add<Concern>` / `Use<Concern>` (Api).
- Tests: `Method_Condition_ExpectedResult` (CA1707 is off for test projects only).
- No placeholder or template names anywhere; scripts/verify searches the repository
  for the placeholder application name and fails if it appears.
