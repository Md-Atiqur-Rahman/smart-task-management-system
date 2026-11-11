using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;
using Datavanced.SmartTaskManagement.Infrastructure.Notifications;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Retention cleanup against a real SQL Server. Every test uses its own migrated database.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class NotificationCleanupTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(Start);

    [Fact]
    public async Task DeleteExpired_RemovesOnlyOlderThanRetention()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database);
        var expired = await AddNotificationAsync(database, recipient.Id, Start.AddDays(-8));
        var nearlyExpired = await AddNotificationAsync(database, recipient.Id, Start.AddDays(-7).AddMinutes(1));
        var fresh = await AddNotificationAsync(database, recipient.Id, Start);

        var deleted = await DeleteExpiredAsync(database);

        deleted.ShouldBe(1);
        var remaining = await RemainingIdsAsync(database);
        remaining.ShouldNotContain(expired);
        remaining.ShouldContain(nearlyExpired);
        remaining.ShouldContain(fresh);
    }

    [Fact]
    public async Task DeleteExpired_RemovesPendingEmailRowsToo()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database);
        var pending = await AddNotificationAsync(database, recipient.Id, Start.AddDays(-30));

        await using (var context = fixture.CreateDbContext(database))
        {
            (await context.Set<Notification>().AsNoTracking().SingleAsync(n => n.Id == pending, _ct))
                .EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        }

        var deleted = await DeleteExpiredAsync(database);

        deleted.ShouldBe(1);
        (await RemainingIdsAsync(database)).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteExpired_RunTwice_IsHarmless()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database);
        await AddNotificationAsync(database, recipient.Id, Start.AddDays(-8));
        var fresh = await AddNotificationAsync(database, recipient.Id, Start);

        (await DeleteExpiredAsync(database)).ShouldBe(1);
        (await DeleteExpiredAsync(database)).ShouldBe(0);

        (await RemainingIdsAsync(database)).ShouldBe([fresh]);
    }

    private async Task<int> DeleteExpiredAsync(DatabaseOptions database)
    {
        await using var context = fixture.CreateDbContext(database);
        var cleanup = new NotificationCleanup(context, Options.Create(new NotificationsOptions()), _time);
        return await cleanup.DeleteExpiredAsync(_ct);
    }

    private async Task<List<Guid>> RemainingIdsAsync(DatabaseOptions database)
    {
        await using var context = fixture.CreateDbContext(database);
        return await context.Set<Notification>().AsNoTracking().Select(n => n.Id).ToListAsync(_ct);
    }

    private async Task<User> AddUserAsync(DatabaseOptions database)
    {
        var user = User.Register(Email.Create($"{Guid.NewGuid():N}@example.com"), "Bob Member", "hash");
        await using var context = fixture.CreateDbContext(database);
        context.Add(user);
        await context.SaveChangesAsync(_ct);
        return user;
    }

    private async Task<Guid> AddNotificationAsync(DatabaseOptions database, Guid recipientId, DateTimeOffset createdAt)
    {
        var notification = Notification.Create(
            recipientId, NotificationType.TaskUnassigned, null, null, "Write docs", "Alice", createdAt);
        await using var context = fixture.CreateDbContext(database);
        context.Add(notification);
        await context.SaveChangesAsync(_ct);
        return notification.Id;
    }
}
