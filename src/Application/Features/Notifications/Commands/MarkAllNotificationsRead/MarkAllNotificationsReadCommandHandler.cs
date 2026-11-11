using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

internal sealed class MarkAllNotificationsReadCommandHandler(
    ISecurityContext securityContext,
    INotificationRepository notifications,
    IUnitOfWork unitOfWork,
    IOptions<NotificationsOptions> options,
    TimeProvider timeProvider,
    LiveEventBuffer liveEvents)
    : ICommandHandler<MarkAllNotificationsReadCommand>
{
    public async Task<Result> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is null)
        {
            return Result.Failure(AuthErrors.TokenRevoked);
        }

        var now = timeProvider.GetUtcNow();
        var unread = await notifications.ListUnreadAsync(
            callerId, now.AddDays(-options.Value.RetentionDays), cancellationToken);

        var changed = false;
        foreach (var notification in unread)
        {
            changed |= notification.MarkRead(now);
        }

        if (!changed)
        {
            return Result.Success();
        }

        liveEvents.Add(new LiveEvent(LiveEventTypes.NotificationsRead, [callerId], false, null));

        var result = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            await liveEvents.FlushAsync(CancellationToken.None);
        }

        return result;
    }
}
