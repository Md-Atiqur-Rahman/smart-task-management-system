using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.ListNotifications;

/// <summary>Read path: security context -> read repository -> DTOs with the rendered message.</summary>
internal sealed class ListNotificationsQueryHandler(
    ISecurityContext securityContext,
    INotificationReadRepository readRepository,
    IOptions<NotificationsOptions> options,
    TimeProvider timeProvider)
    : IQueryHandler<ListNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(
        ListNotificationsQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is null)
        {
            return Result.Failure<IReadOnlyList<NotificationDto>>(AuthErrors.TokenRevoked);
        }

        var settings = options.Value;
        var since = timeProvider.GetUtcNow().AddDays(-settings.RetentionDays);
        var rows = await readRepository.ListAsync(callerId, since, settings.ListLimit, cancellationToken);

        IReadOnlyList<NotificationDto> items =
        [
            .. rows.Select(r => new NotificationDto(
                r.Id,
                r.Type,
                NotificationMessages.Render(r.Type, r.Subject, r.ActorName),
                r.TaskId,
                r.ProjectId,
                r.CreatedAt,
                r.ReadAt is not null)),
        ];
        return Result.Success(items);
    }
}
