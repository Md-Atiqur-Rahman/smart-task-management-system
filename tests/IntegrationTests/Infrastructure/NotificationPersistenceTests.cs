using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Notification persistence against a real SQL Server.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class NotificationPersistenceTests(SqlServerFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Add_Then_GetById_RoundTripsAllFields()
    {
        var recipient = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var created = fixture.Time.GetUtcNow();
        var notification = Notification.Create(recipient, NotificationType.TaskAssigned, taskId, null, "Write docs", "Alice", created);
        notification.MarkRead(created.AddMinutes(1));
        notification.EmailFailed("smtp down", created, 5);
        await SaveAsync(notification);

        await using var context = fixture.CreateDbContext();
        var loaded = await new NotificationRepository(context).GetByIdAsync(notification.Id, _ct);

        loaded.ShouldNotBeNull();
        loaded.RecipientId.ShouldBe(recipient);
        loaded.Type.ShouldBe(NotificationType.TaskAssigned);
        loaded.TaskId.ShouldBe(taskId);
        loaded.ProjectId.ShouldBeNull();
        loaded.Subject.ShouldBe("Write docs");
        loaded.ActorName.ShouldBe("Alice");
        loaded.CreatedAt.ShouldBe(created);
        loaded.ReadAt.ShouldBe(created.AddMinutes(1));
        loaded.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        loaded.EmailAttempts.ShouldBe(1);
        loaded.EmailNextAttemptAt.ShouldBe(created.AddMinutes(2));
        loaded.EmailLastError.ShouldBe("smtp down");
        loaded.EmailClaimedUntil.ShouldBeNull();
        loaded.EmailSentAt.ShouldBeNull();
    }

    [Fact]
    public async Task ListUnread_ReturnsOnlyRecipientsUnreadSince()
    {
        var recipient = Guid.NewGuid();
        var now = fixture.Time.GetUtcNow();
        var since = now.AddDays(-7);
        var fresh = Create(recipient, now);
        var other = Create(Guid.NewGuid(), now);
        var read = Create(recipient, now);
        read.MarkRead(now);
        var old = Create(recipient, since.AddMinutes(-1));
        await SaveAsync(fresh, other, read, old);

        await using var context = fixture.CreateDbContext();
        var result = await new NotificationRepository(context).ListUnreadAsync(recipient, since, _ct);

        result.Select(n => n.Id).ShouldBe([fresh.Id]);
    }

    [Fact]
    public async Task MarkRead_IsPersisted()
    {
        var notification = Create(Guid.NewGuid(), fixture.Time.GetUtcNow());
        await SaveAsync(notification);
        var readAt = fixture.Time.GetUtcNow().AddMinutes(5);

        await using (var context = fixture.CreateDbContext())
        {
            var tracked = (await new NotificationRepository(context).GetByIdAsync(notification.Id, _ct)).ShouldNotBeNull();
            tracked.MarkRead(readAt).ShouldBeTrue();
            (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
        }

        await using var verify = fixture.CreateDbContext();
        var loaded = await new NotificationRepository(verify).GetByIdAsync(notification.Id, _ct);
        loaded.ShouldNotBeNull().ReadAt.ShouldBe(readAt);
    }

    private static Notification Create(Guid recipient, DateTimeOffset createdAt) =>
        Notification.Create(recipient, NotificationType.TaskUnassigned, null, null, "Subject", "Actor", createdAt);

    private async Task SaveAsync(params Notification[] notifications)
    {
        await using var context = fixture.CreateDbContext();
        var repository = new NotificationRepository(context);
        foreach (var notification in notifications)
        {
            repository.Add(notification);
        }

        (await new UnitOfWork(context).SaveChangesAsync(_ct)).IsSuccess.ShouldBeTrue();
    }
}
