using System.Globalization;

using Datavanced.SmartTaskManagement.Application.Abstractions.Email;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Email;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Email;

/// <summary>
/// One cycle of the email outbox (design 8, ADR 0017): claim due notifications with one conditional
/// update, send each claimed one, record the outcome. Two processors (instances) never send the same
/// row: the claim update repeats the full condition, so a row already claimed by another processor no
/// longer matches, and only rows carrying this cycle's claim token are loaded.
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
    /// notifications. An unexpected exception propagates; the claim then expires after
    /// Email:ClaimSeconds and the row is picked up again.
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

        foreach (var notification in batch)
        {
            if (recipients.TryGetValue(notification.RecipientId, out var recipient))
            {
                await SendAsync(notification, recipient.Email.Value, recipient.FullName, settings, ct);
            }
            else
            {
                RecordFailure(notification, "The recipient no longer exists.", settings, "RecipientNotFound");
            }

            // Saved per row: a later unexpected failure never loses the outcome of an earlier send.
            await context.SaveChangesAsync(ct);
        }

        return batch.Count;
    }

    private async Task SendAsync(
        Notification notification, string toAddress, string toName, EmailOptions settings, CancellationToken ct)
    {
        var text = NotificationMessages.Render(notification.Type, notification.Subject, notification.ActorName);
        var link = BuildLink(notification, settings);
        var body = link is null ? text : $"{text}\n\n{link}";
        var message = new EmailMessage(toAddress, toName, $"{settings.FromName}: {text}", body);

        try
        {
            await sender.SendAsync(message, ct);
        }
        catch (Exception ex) when (MailKitEmailSender.IsSendFailure(ex))
        {
            var error = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            RecordFailure(notification, error, settings, ex.GetType().Name);
            return;
        }

        notification.EmailSent(timeProvider.GetUtcNow());
        LogSent(notification.Id);
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
