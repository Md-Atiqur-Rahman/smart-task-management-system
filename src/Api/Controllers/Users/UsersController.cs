using Datavanced.SmartTaskManagement.Api.Auth;
using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Users;
using Datavanced.SmartTaskManagement.Application.Features.Users.Commands.ChangeUserRole;
using Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListAssignableUsers;
using Datavanced.SmartTaskManagement.Application.Features.Users.Queries.ListUsers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Users;

/// <summary>Thin controller: binds the request, calls a handler, maps the Result.</summary>
[ApiController]
[Route(Route)]
public sealed class UsersController : ControllerBase
{
    public const string Route = "api/users";

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpGet]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> List(
        [FromQuery] ListUsersRequest request,
        [FromServices] IQueryHandler<ListUsersQuery, PagedResult<UserDto>> handler,
        [FromServices] IOptions<PagingOptions> paging,
        CancellationToken cancellationToken)
    {
        var query = new ListUsersQuery(
            request.Search,
            request.Role,
            request.SortBy,
            request.SortDirection,
            request.Page,
            request.PageSize ?? paging.Value.DefaultPageSize);

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.ProjectManagerOrAdmin)]
    [HttpGet("assignable")]
    [ProducesResponseType<PagedResult<AssignableUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> ListAssignable(
        [FromQuery] ListAssignableUsersRequest request,
        [FromServices] IQueryHandler<ListAssignableUsersQuery, PagedResult<AssignableUserDto>> handler,
        [FromServices] IOptions<PagingOptions> paging,
        CancellationToken cancellationToken)
    {
        var query = new ListAssignableUsersQuery(
            request.Search,
            request.Page,
            request.PageSize ?? paging.Value.DefaultPageSize);

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("{id:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        [FromBody] ChangeUserRoleRequest request,
        [FromServices] ICommandHandler<ChangeUserRoleCommand> handler,
        CancellationToken cancellationToken)
    {
        if (request.Role is not { } role)
        {
            return ValidationProblem();
        }

        var result = await handler.Handle(new ChangeUserRoleCommand(id, role), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
