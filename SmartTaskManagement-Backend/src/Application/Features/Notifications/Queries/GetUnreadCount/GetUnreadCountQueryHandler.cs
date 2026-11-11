using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.GetUnreadCount;

internal sealed class GetUnreadCountQueryHandler(
    ISecurityContext securityContext,
    INotificationReadRepository readRepository,
    IOptions<NotificationsOptions> options,
    TimeProvider timeProvider)
    : IQueryHandler<GetUnreadCountQuery, UnreadCountDto>
{
    public async Task<Result<UnreadCountDto>> Handle(GetUnreadCountQuery query, CancellationToken cancellationToken)
    {
        if (securityContext.UserId is not { } callerId || securityContext.Role is null)
        {
            return Result.Failure<UnreadCountDto>(AuthErrors.TokenRevoked);
        }

        var since = timeProvider.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return Result.Success(new UnreadCountDto(
            await readRepository.CountUnreadAsync(callerId, since, cancellationToken)));
    }
}
