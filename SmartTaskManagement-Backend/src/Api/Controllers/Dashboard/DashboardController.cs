using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Features.Dashboard;
using Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetDashboardSummary;
using Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetUpcomingTasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Dashboard;

/// <summary>Thin controller: calls a query handler and maps the Result.</summary>
[ApiController]
[Route(Route)]
public sealed class DashboardController : ControllerBase
{
    public const string Route = "api/dashboard";

    [Authorize]
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> GetSummary(
        [FromServices] IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetDashboardSummaryQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("upcoming")]
    [ProducesResponseType<IReadOnlyList<UpcomingTaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> GetUpcoming(
        [FromServices] IQueryHandler<GetUpcomingTasksQuery, IReadOnlyList<UpcomingTaskDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetUpcomingTasksQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
