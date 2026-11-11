using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using Datavanced.SmartTaskManagement.Application.Features.Notifications.Commands.MarkNotificationRead;
using Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.GetUnreadCount;
using Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.ListNotifications;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Notifications;

/// <summary>Thin controller: calls a handler and maps the Result. Only the caller's own notifications.</summary>
[ApiController]
[Route(Route)]
public sealed class NotificationsController : ControllerBase
{
    public const string Route = "api/notifications";

    [Authorize]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<NotificationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> List(
        [FromServices] IQueryHandler<ListNotificationsQuery, IReadOnlyList<NotificationDto>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ListNotificationsQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> GetUnreadCount(
        [FromServices] IQueryHandler<GetUnreadCountQuery, UnreadCountDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetUnreadCountQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> MarkRead(
        Guid id,
        [FromServices] ICommandHandler<MarkNotificationReadCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new MarkNotificationReadCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> MarkAllRead(
        [FromServices] ICommandHandler<MarkAllNotificationsReadCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new MarkAllNotificationsReadCommand(), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
