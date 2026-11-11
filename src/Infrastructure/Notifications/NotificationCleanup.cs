using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Notifications;

/// <summary>
/// Retention (design 9): one set-based delete of every notification older than
/// Notifications:RetentionDays, whatever its email status. Safe to run on several instances at once.
/// </summary>
public sealed class NotificationCleanup(
    AppDbContext context,
    IOptions<NotificationsOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>Deletes expired notifications and returns how many rows were removed.</summary>
    public async Task<int> DeleteExpiredAsync(CancellationToken ct)
    {
        var threshold = timeProvider.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return await context.Set<Notification>()
            .Where(n => n.CreatedAt < threshold)
            .ExecuteDeleteAsync(ct);
    }
}
