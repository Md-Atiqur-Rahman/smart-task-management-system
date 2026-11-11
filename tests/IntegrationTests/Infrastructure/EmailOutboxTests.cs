using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Mail;
using Datavanced.SmartTaskManagement.Infrastructure.Mail;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

using MailKit.Net.Smtp;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// The email outbox against a real SQL Server. Every test uses its own migrated database, so pending
/// notifications of other tests can never be claimed here.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class EmailOutboxTests(SqlServerFixture fixture)
{
    private const string AppBaseUrl = "http://localhost:4200";

    private static readonly DateTimeOffset Start = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(Start);

    private readonly FakeEmailSender _sender = new();

    [Fact]
    public async Task Process_SendsPendingAndMarksSent()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Bob Member");
        var notification = await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Write docs");

        var processed = await ProcessAsync(database);

        processed.ShouldBe(1);
        var message = _sender.Sent.ShouldHaveSingleItem();
        message.ToAddress.ShouldBe(recipient.Email.Value);
        message.ToName.ShouldBe("Bob Member");
        message.Subject.ShouldBe("Task notifications: Alice removed the task \"Write docs\" from you.");
        message.Body.ShouldBe("Alice removed the task \"Write docs\" from you.");

        var stored = await LoadAsync(database, notification.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Sent);
        stored.EmailSentAt.ShouldBe(Start);
        stored.EmailAttempts.ShouldBe(0);
        stored.EmailClaimedUntil.ShouldBeNull();

        (await ProcessAsync(database)).ShouldBe(0);
        _sender.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Process_TwoProcessorsConcurrently_EachRowSentOnce()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Carol Member");
        var ids = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var notification = await AddNotificationAsync(
                database, recipient.Id, NotificationType.TaskUnassigned, null, null, $"Task {i}");
            ids.Add(notification.Id);
        }

        await using var first = fixture.CreateDbContext(database);
        await using var second = fixture.CreateDbContext(database);
        var results = await Task.WhenAll(
            Processor(first, Settings()).ProcessBatchAsync(_ct),
            Processor(second, Settings()).ProcessBatchAsync(_ct));

        results.Sum().ShouldBe(10);
        _sender.Sent.Count.ShouldBe(10);
        _sender.Sent.Select(m => m.Subject).Distinct(StringComparer.Ordinal).Count().ShouldBe(10);
        foreach (var id in ids)
        {
            (await LoadAsync(database, id)).EmailStatus.ShouldBe(EmailDeliveryStatus.Sent);
        }
    }

    [Fact]
    public async Task Process_SendFails_SchedulesRetryWithBackoff()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Dan Member");
        var notification = await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Retry me");
        _sender.Failure = _ => SmtpFailure();

        (await ProcessAsync(database)).ShouldBe(1);

        var stored = await LoadAsync(database, notification.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        stored.EmailAttempts.ShouldBe(1);
        stored.EmailNextAttemptAt.ShouldBe(Start.AddMinutes(2));
        stored.EmailLastError.ShouldNotBeNullOrWhiteSpace();
        stored.EmailClaimedUntil.ShouldBeNull();

        // Not due yet: nothing is claimed.
        _time.Advance(TimeSpan.FromMinutes(1));
        (await ProcessAsync(database)).ShouldBe(0);
        _sender.Attempts.ShouldBe(1);

        // Due: the second attempt fails too and doubles the delay (2^2 minutes).
        _time.Advance(TimeSpan.FromMinutes(1));
        (await ProcessAsync(database)).ShouldBe(1);
        stored = await LoadAsync(database, notification.Id);
        stored.EmailAttempts.ShouldBe(2);
        stored.EmailNextAttemptAt.ShouldBe(Start.AddMinutes(2 + 4));

        // The server is back: the third attempt is sent.
        _sender.Failure = null;
        _time.Advance(TimeSpan.FromMinutes(4));
        (await ProcessAsync(database)).ShouldBe(1);
        (await LoadAsync(database, notification.Id)).EmailStatus.ShouldBe(EmailDeliveryStatus.Sent);
        _sender.Sent.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Process_FailsMaxAttempts_IsFailedAndNotRetried()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Eve Member");
        var notification = await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Give up");
        _sender.Failure = _ => new IOException("connection reset");
        var options = Settings(maxAttempts: 2);

        (await ProcessAsync(database, options)).ShouldBe(1);
        _time.Advance(TimeSpan.FromMinutes(2));
        (await ProcessAsync(database, options)).ShouldBe(1);

        var stored = await LoadAsync(database, notification.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Failed);
        stored.EmailAttempts.ShouldBe(2);
        stored.EmailLastError.ShouldNotBeNull().ShouldContain("connection reset");

        _sender.Failure = null;
        _time.Advance(TimeSpan.FromDays(1));
        (await ProcessAsync(database, options)).ShouldBe(0);
        _sender.Attempts.ShouldBe(2);
        _sender.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Process_ExpiredClaim_IsPickedUpAgain()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Fay Member");
        var notification = await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Crash");

        // Shutdown in the middle of the send: nothing is recorded, the claim stays until it expires.
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        _sender.Failure = _ =>
        {
            shutdown.Cancel();
            return new OperationCanceledException(shutdown.Token);
        };
        await Should.ThrowAsync<OperationCanceledException>(() => ProcessAsync(database, ct: shutdown.Token));
        var stored = await LoadAsync(database, notification.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        stored.EmailAttempts.ShouldBe(0);
        stored.EmailClaimedUntil.ShouldBe(Start.AddSeconds(120));

        _sender.Failure = null;
        _time.Advance(TimeSpan.FromSeconds(60));
        (await ProcessAsync(database)).ShouldBe(0);
        _sender.Sent.ShouldBeEmpty();

        _time.Advance(TimeSpan.FromSeconds(61));
        (await ProcessAsync(database)).ShouldBe(1);
        _sender.Sent.ShouldHaveSingleItem();
        (await LoadAsync(database, notification.Id)).EmailStatus.ShouldBe(EmailDeliveryStatus.Sent);
    }

    [Fact]
    public async Task Process_UnexpectedFailure_IsCountedAndOtherRowsAreSent()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Hal Member");
        // The poison row is due first, so it is handled first in the batch.
        var poison = await AddNotificationAsync(
            database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Poison", Start.AddMinutes(-1));
        await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Healthy 1");
        await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Healthy 2");
        _sender.Failure = m => m.Subject.Contains("Poison", StringComparison.Ordinal)
            ? new InvalidOperationException("unexpected")
            : null;
        var options = Settings(maxAttempts: 2);

        (await ProcessAsync(database, options)).ShouldBe(3);

        _sender.Sent.Count.ShouldBe(2);
        var stored = await LoadAsync(database, poison.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        stored.EmailAttempts.ShouldBe(1);
        stored.EmailLastError.ShouldBe(nameof(InvalidOperationException));
        stored.EmailClaimedUntil.ShouldBeNull();

        _time.Advance(TimeSpan.FromMinutes(2));
        (await ProcessAsync(database, options)).ShouldBe(1);
        stored = await LoadAsync(database, poison.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Failed);
        stored.EmailAttempts.ShouldBe(2);

        _time.Advance(TimeSpan.FromDays(1));
        (await ProcessAsync(database, options)).ShouldBe(0);
        _sender.Attempts.ShouldBe(4);
    }

    [Fact]
    public async Task Process_ClaimExpiresDuringBatch_RemainingRowWaitsForNextCycle()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Ivy Member");
        await AddNotificationAsync(
            database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Slow", Start.AddMinutes(-1));
        var waiting = await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Waiting");

        // The first send takes longer than the claim.
        _sender.Failure = m =>
        {
            if (m.Subject.Contains("Slow", StringComparison.Ordinal))
            {
                _time.Advance(TimeSpan.FromSeconds(121));
            }

            return null;
        };

        (await ProcessAsync(database)).ShouldBe(1);

        _sender.Sent.ShouldHaveSingleItem().Subject.ShouldContain("Slow");
        var stored = await LoadAsync(database, waiting.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        stored.EmailAttempts.ShouldBe(0);
        stored.EmailClaimedUntil.ShouldBe(Start.AddSeconds(120));

        (await ProcessAsync(database)).ShouldBe(1);
        _sender.Sent.Count.ShouldBe(2);
        (await LoadAsync(database, waiting.Id)).EmailStatus.ShouldBe(EmailDeliveryStatus.Sent);
    }

    [Fact]
    public async Task Process_RemainingClaimShorterThanSendBudget_RowWaitsUntilClaimExpires()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Joy Member");
        await AddNotificationAsync(
            database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Slow", Start.AddMinutes(-1));
        var waiting = await AddNotificationAsync(database, recipient.Id, NotificationType.TaskUnassigned, null, null, "Waiting");

        // The claim (120 s) has not expired after the first send, but only 59 s remain: less than the
        // send budget (ClaimSeconds / 2 = 60 s), so a second send could outlive the claim.
        _sender.Failure = m =>
        {
            if (m.Subject.Contains("Slow", StringComparison.Ordinal))
            {
                _time.Advance(TimeSpan.FromSeconds(61));
            }

            return null;
        };

        (await ProcessAsync(database)).ShouldBe(1);

        _sender.Sent.ShouldHaveSingleItem().Subject.ShouldContain("Slow");
        var stored = await LoadAsync(database, waiting.Id);
        stored.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        stored.EmailAttempts.ShouldBe(0);
        stored.EmailClaimedUntil.ShouldBe(Start.AddSeconds(120));

        // Still claimed: no processor may take it yet.
        (await ProcessAsync(database)).ShouldBe(0);
        _sender.Sent.Count.ShouldBe(1);

        // The claim expired: the row is claimed again and sent once.
        _time.Advance(TimeSpan.FromSeconds(60));
        (await ProcessAsync(database)).ShouldBe(1);
        _sender.Sent.Count.ShouldBe(2);
        (await LoadAsync(database, waiting.Id)).EmailStatus.ShouldBe(EmailDeliveryStatus.Sent);
    }

    [Fact]
    public async Task Process_LinkUsesTemplate()
    {
        var database = await fixture.CreateIsolatedDatabaseAsync(_ct);
        var recipient = await AddUserAsync(database, "Gus Member");
        var taskId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await AddNotificationAsync(database, recipient.Id, NotificationType.TaskAssigned, taskId, null, "Linked task");
        await AddNotificationAsync(database, recipient.Id, NotificationType.ProjectOwnerAssigned, null, projectId, "Linked project");

        (await ProcessAsync(database)).ShouldBe(2);

        var task = _sender.Sent.Single(m => m.Subject.Contains("Linked task", StringComparison.Ordinal));
        task.Body.ShouldStartWith("Alice assigned you the task \"Linked task\".");
        task.Body.ShouldContain($"{AppBaseUrl}/tasks/{taskId}");
        var project = _sender.Sent.Single(m => m.Subject.Contains("Linked project", StringComparison.Ordinal));
        project.Body.ShouldContain($"{AppBaseUrl}/projects/{projectId}");
    }

    private static SmtpCommandException SmtpFailure() =>
        new(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxBusy, "mailbox busy");

    private static EmailOptions Settings(int maxAttempts = 5) => new()
    {
        Host = "localhost",
        Port = 1025,
        UseTls = false,
        FromAddress = "notifications@example.com",
        FromName = "Task notifications",
        AppBaseUrl = new Uri(AppBaseUrl),
        BatchSize = 20,
        ClaimSeconds = 120,
        MaxAttempts = maxAttempts,
    };

    private EmailOutboxProcessor Processor(AppDbContext context, EmailOptions options) =>
        new(context, _sender, Options.Create(options), _time,
            NullLogger<EmailOutboxProcessor>.Instance);

    private async Task<int> ProcessAsync(
        DatabaseOptions database, EmailOptions? options = null, CancellationToken? ct = null)
    {
        await using var context = fixture.CreateDbContext(database);
        return await Processor(context, options ?? Settings()).ProcessBatchAsync(ct ?? _ct);
    }

    private async Task<User> AddUserAsync(DatabaseOptions database, string fullName)
    {
        var user = User.Register(Email.Create($"{Guid.NewGuid():N}@example.com"), fullName, "hash");
        await using var context = fixture.CreateDbContext(database);
        context.Add(user);
        await context.SaveChangesAsync(_ct);
        return user;
    }

    private async Task<Notification> AddNotificationAsync(
        DatabaseOptions database,
        Guid recipientId,
        NotificationType type,
        Guid? taskId,
        Guid? projectId,
        string subject,
        DateTimeOffset? createdAt = null)
    {
        var notification = Notification.Create(recipientId, type, taskId, projectId, subject, "Alice", createdAt ?? Start);
        await using var context = fixture.CreateDbContext(database);
        context.Add(notification);
        await context.SaveChangesAsync(_ct);
        return notification;
    }

    private async Task<Notification> LoadAsync(DatabaseOptions database, Guid id)
    {
        await using var context = fixture.CreateDbContext(database);
        return await context.Set<Notification>().AsNoTracking().SingleAsync(n => n.Id == id, _ct);
    }
}
