namespace Datavanced.SmartTaskManagement.Application.Abstractions.Email;

/// <summary>
/// Sends one email. Called only by the email outbox (ADR 0017), never inside a command. A failed send
/// throws; the outbox decides which failures are retried.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
