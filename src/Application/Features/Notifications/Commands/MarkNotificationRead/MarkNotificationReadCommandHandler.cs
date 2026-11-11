using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Commands.MarkNotificationRead;

/// <summary>
/// Another user's notification, or one older than the retention window, is "not found".
/// Reading an already read notification succeeds and publishes nothing.
/// </summary>
internal sealed class MarkNotificationReadCommandHandler(
    ISecurityContext securityContext,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    IOptions<NotificationsOptions> options,
    TimeProvider timeProvider,
    LiveEventBuffer liveEvents)
    : ICommandHandler<MarkNotificationReadCommand>
{
    public async Task<Result> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is null)
        {
            return Result.Failure(AuthErrors.TokenRevoked);
        }

        var now = timeProvider.GetUtcNow();
        var notification = await notifications.GetByIdAsync(command.Id, cancellationToken);
        if (notification is null
            || notification.RecipientId != callerId
            || notification.CreatedAt < now.AddDays(-options.Value.RetentionDays))
        {
            return Result.Failure(NotificationErrors.NotFound);
        }

        if (!notification.MarkRead(now))
        {
            return Result.Success();
        }

        liveEvents.Add(new LiveEvent(LiveEventTypes.NotificationsRead, [callerId], false, null));

        var result = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            // Not the request token: a request aborted right after a successful save still publishes.
            await liveEvents.FlushAsync(CancellationToken.None);
        }

        return result;
    }
}
