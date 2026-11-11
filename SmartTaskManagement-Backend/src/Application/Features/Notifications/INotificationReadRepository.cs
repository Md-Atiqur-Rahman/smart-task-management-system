namespace Datavanced.SmartTaskManagement.Application.Features.Notifications;

/// <summary>Read side of notifications. Implemented in Infrastructure; only the recipient's own rows.</summary>
public interface INotificationReadRepository
{
    /// <summary>Newest first, created at or after <paramref name="since"/>, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<NotificationRow>> ListAsync(
        Guid recipientId, DateTimeOffset since, int limit, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid recipientId, DateTimeOffset since, CancellationToken cancellationToken);
}
