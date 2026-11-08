# Api layer rules

- Composition root: references Application and Infrastructure; no business logic.
- `Program.cs` only calls `ConfigureApplication()`, `ConfigureInfrastructure()`
  and the `Add*`/`Use*` extensions; no inline registration (architecture test).
- DI style: `Add<Concern>(this IServiceCollection services[, IConfiguration
  configuration])` and `Use<Concern>(this WebApplication app)` in
  `Extensions/<Concern>Extensions.cs`. Configuration is read only there and bound
  immediately to Options with `ValidateOnStart()`. No `Configure*` methods here.
- Controllers in `Controllers/<Module>/` are thin: bind the request, call the
  handler interface (`[FromServices]`), map the `Result` (`Ok`/`Created`/
  `NoContent` or `this.ToProblem(error)`). No repositories, `IUnitOfWork` or
  `DbContext`.
- Every action: `[Authorize]` or `[AllowAnonymous]` (anonymous only in the
  Sample namespace, marked `// SAMPLE ONLY: replace with [Authorize] in Phase 2`)
  and `[ProducesResponseType]` for every status, with `application/problem+json`
  on errors. No class-level `[Produces]`.
- Request bodies bind to request records with nullable fields; validation
  happens in the Application pipeline.
- Only controllers and their request/response types are public; everything else
  is `internal` (CA1515 is off only for `Controllers/`; architecture test).
- After changing controllers or DTOs: `bash scripts/generate-openapi.sh` and
  commit `docs/openapi.json`.
- Swagger UI and `/openapi/v1.json` are Development only; `/health` is the only
  health endpoint.
