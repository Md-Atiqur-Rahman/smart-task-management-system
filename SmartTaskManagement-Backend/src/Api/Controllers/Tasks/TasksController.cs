using Datavanced.SmartTaskManagement.Api.Auth;
using Datavanced.SmartTaskManagement.Api.Controllers.Samples;
using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Tasks;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.AssignTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ChangeTaskStatus;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.CreateTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.DeleteTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ImproveTaskDescription;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.UpdateTask;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.GetTaskById;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListProjectTasks;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListTasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Tasks;

/// <summary>Thin controller: binds the request, calls a handler, maps the Result.</summary>
[ApiController]
public sealed class TasksController : ControllerBase
{
    public const string Route = "api/tasks";
    public const string ProjectTasksRoute = "/api/projects/{projectId:guid}/tasks";
    public const string ByIdRoute = "api/tasks/{id:guid}";
    public const string ImproveDescriptionRoute = "api/tasks/description/improve";

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpPost(ProjectTasksRoute)]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateTaskRequest request,
        [FromServices] ICommandHandler<CreateTaskCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateTaskCommand(
                projectId,
                request.Title ?? string.Empty,
                request.Description,
                request.Priority,
                request.DueDate,
                request.AssigneeId),
            cancellationToken);

        return result.IsSuccess
            ? Created(new Uri($"/{Route}/{result.Value}", UriKind.Relative), new CreatedResponse(result.Value))
            : this.ToProblem(result.Error);
    }

    /// <summary>
    /// Returns an improved version of a task description from the AI service (design 3). Saves nothing:
    /// the client shows the result and the PM decides whether to use it.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpPost(ImproveDescriptionRoute)]
    [ProducesResponseType<ImprovedDescriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> ImproveDescription(
        [FromBody] ImproveDescriptionRequest request,
        [FromServices] ICommandHandler<ImproveTaskDescriptionCommand, ImprovedDescriptionDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new ImproveTaskDescriptionCommand(request.Title, request.Description ?? string.Empty),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet(ProjectTasksRoute)]
    [ProducesResponseType<PagedResult<TaskListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> ListProjectTasks(
        Guid projectId,
        [FromQuery] ListProjectTasksRequest request,
        [FromServices] IQueryHandler<ListProjectTasksQuery, PagedResult<TaskListItemDto>> handler,
        [FromServices] IOptions<PagingOptions> paging,
        CancellationToken cancellationToken)
    {
        var query = new ListProjectTasksQuery(
            projectId,
            request.Search,
            request.Status,
            request.Priority,
            request.AssigneeId,
            request.Unassigned,
            request.DueFrom,
            request.DueTo,
            request.SortBy,
            request.SortDirection,
            request.Page,
            request.PageSize ?? paging.Value.DefaultPageSize);

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet(Route)]
    [ProducesResponseType<PagedResult<TaskListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> List(
        [FromQuery] ListTasksRequest request,
        [FromServices] IQueryHandler<ListTasksQuery, PagedResult<TaskListItemDto>> handler,
        [FromServices] IOptions<PagingOptions> paging,
        CancellationToken cancellationToken)
    {
        var query = new ListTasksQuery(
            request.Search,
            request.Status,
            request.Priority,
            request.AssigneeId,
            request.Unassigned,
            request.DueFrom,
            request.DueTo,
            request.SortBy,
            request.SortDirection,
            request.Page,
            request.PageSize ?? paging.Value.DefaultPageSize,
            request.ProjectId);

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet(ByIdRoute)]
    [ProducesResponseType<TaskDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetTaskByIdQuery, TaskDetailsDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetTaskByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpPut(ByIdRoute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTaskRequest request,
        [FromServices] ICommandHandler<UpdateTaskCommand> handler,
        CancellationToken cancellationToken)
    {
        if (request.Priority is not { } priority)
        {
            return ValidationProblem();
        }

        var result = await handler.Handle(
            new UpdateTaskCommand(id, request.Title ?? string.Empty, request.Description, priority, request.DueDate),
            cancellationToken);

        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpDelete(ByIdRoute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteTaskCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteTaskCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpPut(ByIdRoute + "/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeTaskStatusRequest request,
        [FromServices] ICommandHandler<ChangeTaskStatusCommand> handler,
        CancellationToken cancellationToken)
    {
        if (request.Status is not { } status)
        {
            return ValidationProblem();
        }

        var result = await handler.Handle(new ChangeTaskStatusCommand(id, status), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpPut(ByIdRoute + "/assignee")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignTaskRequest request,
        [FromServices] ICommandHandler<AssignTaskCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new AssignTaskCommand(id, request.AssigneeId), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
