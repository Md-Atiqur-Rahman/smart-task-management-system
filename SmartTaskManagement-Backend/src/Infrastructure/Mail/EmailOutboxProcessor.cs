using System.Globalization;

using Datavanced.SmartTaskManagement.Application.Abstractions.Email;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Mail;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Mail;

/// <summary>
/// One cycle of the email outbox (design 8, ADR 0017): claim due notifications with one conditional
/// update, send each claimed one, record the outcome. Two processors (instances) never send the same
/// row while its claim holds: the claim update repeats the full condition, so a row already claimed by
/// another processor no longer matches, only rows carrying this cycle's claim token are loaded, and a
/// send starts only when it can finish inside the claim. Delivery is at-least-once: a crash between
/// the send and the save of "sent" sends the row again after the claim expires.
/// </summary>
public sealed partial class EmailOutboxProcessor(
    AppDbContext context,
    IEmailSender sender,
    IOptions<EmailOptions> options,
    TimeProvider timeProvider,
    ILogger<EmailOutboxProcessor> logger)
{
    /// <summary>
    /// Claims up to Email:BatchSize due notifications and sends them. Returns the number of claimed
    /// notifications handled in this cycle (a row whose remaining claim is shorter than
    /// Email:ClaimSeconds / 2, the send budget, is left for later).
    /// A failure of one row is recorded on that row; only cancellation and database errors propagate,
    /// and the claim then expires after Email:ClaimSeconds.
    /// </summary>
    public async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var claimedUntil = now.AddSeconds(settings.ClaimSeconds);
        var token = Guid.NewGuid();

        var notifications = context.Set<Notification>();
        var candidates = await notifications
            .Where(n => n.EmailStatus == EmailDeliveryStatus.Pending
                && n.EmailNextAttemptAt <= now
                && (n.EmailClaimedUntil == null || n.EmailClaimedUntil < now))
            .OrderBy(n => n.EmailNextAttemptAt)
            .Select(n => n.Id)
            .Take(settings.BatchSize)
            .ToListAsync(ct);
        if (candidates.Count == 0)
        {
            return 0;
        }

        // The same condition again: a row another processor claimed in the meantime is not changed.
        var claimed = await notifications
            .Where(n => candidates.Contains(n.Id)
                && n.EmailStatus == EmailDeliveryStatus.Pending
                && n.EmailNextAttemptAt <= now
                && (n.EmailClaimedUntil == null || n.EmailClaimedUntil < now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.EmailClaimedUntil, claimedUntil)
                    .SetProperty(n => EF.Property<Guid?>(n, NotificationConfiguration.EmailClaimTokenProperty), token),
                ct);
        if (claimed == 0)
        {
            return 0;
        }

        var batch = await notifications
            .Where(n => EF.Property<Guid?>(n, NotificationConfiguration.EmailClaimTokenProperty) == token)
            .OrderBy(n => n.EmailNextAttemptAt)
            .ToListAsync(ct);

        // Current address and name, loaded at send time (a changed name or address is used).
        var recipientIds = batch.Select(n => n.RecipientId).Distinct().ToList();
        var recipients = await context.Set<User>()
            .AsNoTracking()
            .Where(u => recipientIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.FullName })
            .ToDictionaryAsync(u => u.Id, ct);

        var handled = 0;
        foreach (var notification in batch)
        {
            // Too little of the claim is left for one whole send (the sender stops a send after
            // SendBudget): starting it could outlive the claim, and another processor could then claim
            // and send the same row. Leave it unchanged; it is claimed again once the claim expires.
            if (notification.EmailClaimedUntil - timeProvider.GetUtcNow() < settings.SendBudget)
            {
                continue;
            }

            handled++;
            if (recipients.TryGetValue(notification.RecipientId, out var recipient))
            {
                await SendAsync(notification, recipient.Email.Value, recipient.FullName, settings, ct);
            }
            else
            {
                RecordFailure(notification, "The recipient no longer exists.", settings, "RecipientNotFound");
                await context.SaveChangesAsync(ct);
            }
        }

        return handled;
    }

    /// <summary>
    /// Sends one row and saves its outcome (per row: a later failure never loses an earlier outcome).
    /// Every failure except cancellation counts as an attempt, so one bad row never blocks the others and
    /// reaches Failed after MaxAttempts.
    /// </summary>
    private async Task SendAsync(
        Notification notification, string toAddress, string toName, EmailOptions settings, CancellationToken ct)
    {
        try
        {
            var text = NotificationMessages.Render(notification.Type, notification.Subject, notification.ActorName);
            var link = BuildLink(notification, settings);
            var body = link is null ? text : $"{text}\n\n{link}";
            await sender.SendAsync(new EmailMessage(toAddress, toName, $"{settings.FromName}: {text}", body), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown: nothing is recorded; the claim expires and the row is picked up again.
            throw;
        }
        catch (Exception ex) when (MailKitEmailSender.IsSendFailure(ex))
        {
            var error = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            RecordFailure(notification, error, settings, ex.GetType().Name);
            await context.SaveChangesAsync(ct);
            return;
        }
#pragma warning disable CA1031 // Design 8: an unexpected per-row failure (bug, bad address, rendering) is logged and counted as an attempt, so the rest of the batch is still sent and the row reaches Failed after MaxAttempts instead of blocking the outbox.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Only the type name: the message of an unknown exception may quote the recipient address.
            var reason = ex.GetType().Name;
            notification.EmailFailed(reason, timeProvider.GetUtcNow(), settings.MaxAttempts);
            LogUnexpectedFailure(notification.Id, notification.EmailAttempts, reason);
            await context.SaveChangesAsync(ct);
            return;
        }

        notification.EmailSent(timeProvider.GetUtcNow());
        LogSent(notification.Id);

        // Not cancellable: a shutdown between the send and this save must not cause a second send.
        await context.SaveChangesAsync(CancellationToken.None);
    }

    // The exception itself is not logged: an SMTP server reply can quote the recipient address.
    // Its message is kept in EmailLastError (database only).
    private void RecordFailure(Notification notification, string error, EmailOptions settings, string reason)
    {
        notification.EmailFailed(error, timeProvider.GetUtcNow(), settings.MaxAttempts);
        if (notification.EmailStatus == EmailDeliveryStatus.Failed)
        {
            LogFailedForGood(notification.Id, notification.EmailAttempts, reason);
        }
        else
        {
            LogRetryScheduled(notification.Id, notification.EmailAttempts, notification.EmailNextAttemptAt, reason);
        }
    }

    private static string? BuildLink(Notification notification, EmailOptions settings)
    {
        string template;
        Guid id;
        if (notification.TaskId is { } taskId)
        {
            (template, id) = (settings.TaskPathTemplate, taskId);
        }
        else if (notification.ProjectId is { } projectId)
        {
            (template, id) = (settings.ProjectPathTemplate, projectId);
        }
        else
        {
            return null;
        }

        var path = template.Replace(
            EmailOptions.IdPlaceholder, id.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal);
        // AppBaseUrl is validated at startup ([Required], absolute http or https).
        var baseUrl = settings.AppBaseUrl?.AbsoluteUri.TrimEnd('/') ?? string.Empty;
        return baseUrl + path;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Notification email {NotificationId} failed unexpectedly ({Reason}, attempt {Attempts}).")]
    private partial void LogUnexpectedFailure(Guid notificationId, int attempts, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification email {NotificationId} sent.")]
    private partial void LogSent(Guid notificationId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Notification email {NotificationId} failed ({Reason}, attempt {Attempts}); next attempt at {NextAttemptAt}.")]
    private partial void LogRetryScheduled(Guid notificationId, int attempts, DateTimeOffset nextAttemptAt, string reason);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Notification email {NotificationId} failed ({Reason}) after {Attempts} attempts and will not be retried.")]
    private partial void LogFailedForGood(Guid notificationId, int attempts, string reason);
}
