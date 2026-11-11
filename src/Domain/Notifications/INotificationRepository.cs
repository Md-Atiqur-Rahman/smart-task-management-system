namespace Datavanced.SmartTaskManagement.Domain.Notifications;

/// <summary>Write repository of the Notification aggregate. Changes are saved by IUnitOfWork.</summary>
public interface INotificationRepository
{
    void Add(Notification notification);

    /// <summary>Tracked aggregate, or null when it does not exist.</summary>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked unread notifications of the recipient created at or after <paramref name="since"/>.</summary>
    Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid recipientId, DateTimeOffset since, CancellationToken cancellationToken);
}
