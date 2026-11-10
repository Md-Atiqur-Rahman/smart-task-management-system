using Datavanced.SmartTaskManagement.Api.Auth;
using Datavanced.SmartTaskManagement.Api.Controllers.Samples;
using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.CreateProject;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.DeleteProject;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.UpdateProject;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.GetProjectById;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Projects;

/// <summary>Thin controller: binds the request, calls a handler, maps the Result.</summary>
[ApiController]
[Route(Route)]
public sealed class ProjectsController : ControllerBase
{
    public const string Route = "api/projects";

    [Authorize]
    [HttpGet]
    [ProducesResponseType<PagedResult<ProjectListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> List(
        [FromQuery] ListProjectsRequest request,
        [FromServices] IQueryHandler<ListProjectsQuery, PagedResult<ProjectListItemDto>> handler,
        [FromServices] IOptions<PagingOptions> paging,
        CancellationToken cancellationToken)
    {
        var query = new ListProjectsQuery(
            request.Search,
            request.OwnerId,
            request.SortBy,
            request.SortDirection,
            request.Page,
            request.PageSize ?? paging.Value.DefaultPageSize);

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProjectDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetProjectByIdQuery, ProjectDetailsDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetProjectByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProjectRequest request,
        [FromServices] ICommandHandler<CreateProjectCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateProjectCommand(request.Name ?? string.Empty, request.Description, request.OwnerId),
            cancellationToken);

        return result.IsSuccess
            ? Created(new Uri($"/{Route}/{result.Value}", UriKind.Relative), new CreatedResponse(result.Value))
            : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProjectRequest request,
        [FromServices] ICommandHandler<UpdateProjectCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new UpdateProjectCommand(id, request.Name ?? string.Empty, request.Description),
            cancellationToken);

        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteProjectCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteProjectCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
