using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>Notification reads: only the recipient's own rows, AsNoTracking, projected to rows in SQL.</summary>
public sealed class NotificationReadRepository(AppDbContext context) : INotificationReadRepository
{
    public async Task<IReadOnlyList<NotificationRow>> ListAsync(
        Guid recipientId, DateTimeOffset since, int limit, CancellationToken cancellationToken) =>
        await context.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.RecipientId == recipientId && n.CreatedAt >= since)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(limit)
            .Select(n => new NotificationRow(
                n.Id, n.Type, n.Subject, n.ActorName, n.TaskId, n.ProjectId, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(Guid recipientId, DateTimeOffset since, CancellationToken cancellationToken) =>
        context.Set<Notification>()
            .AsNoTracking()
            .CountAsync(n => n.RecipientId == recipientId && n.CreatedAt >= since && n.ReadAt == null, cancellationToken);
}
