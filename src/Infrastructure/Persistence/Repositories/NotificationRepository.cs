using Datavanced.SmartTaskManagement.Domain.Notifications;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(AppDbContext context) : INotificationRepository
{
    public void Add(Notification notification) => context.Set<Notification>().Add(notification);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<Notification>().SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid recipientId, DateTimeOffset since, CancellationToken cancellationToken) =>
        await context.Set<Notification>()
            .Where(n => n.RecipientId == recipientId && n.ReadAt == null && n.CreatedAt >= since)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
}
