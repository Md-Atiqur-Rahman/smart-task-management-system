using System.Globalization;
using System.Net.Sockets;

using Datavanced.SmartTaskManagement.Application.Abstractions.Email;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Mail;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace Datavanced.SmartTaskManagement.Infrastructure.Mail;

/// <summary>
/// Sends one plain-text email over SMTP with MailKit 5.0: connect (STARTTLS when Email:UseTls),
/// authenticate when Email:UserName is set, send, disconnect. The server certificate is always
/// validated by MailKit's default callback; it is never replaced. One whole send is limited to
/// <see cref="EmailOptions.SendBudget"/>, so it always ends inside the outbox claim.
/// </summary>
public sealed class MailKitEmailSender(IOptions<EmailOptions> options, TimeProvider timeProvider) : IEmailSender
{
    /// <summary>
    /// True for the failures MailKit 5.0 documents for SmtpClient.ConnectAsync, AuthenticateAsync and
    /// SendAsync that mean "the mail server or the network refused or broke the send": command and
    /// protocol errors, authentication and SASL errors, TLS handshake errors, a missing authentication,
    /// an address the server cannot take, socket and I/O errors. The outbox retries these; any other
    /// exception is a bug and is not counted as an attempt.
    /// </summary>
    public static bool IsSendFailure(Exception exception) => exception is
        CommandException // SmtpCommandException
        or ProtocolException // SmtpProtocolException
        or AuthenticationException
        or SaslException
        or SslHandshakeException
        or ServiceNotAuthenticatedException
        or SmtpUtf8AddressException
        or SocketException
        or IOException
        // Defensive: MailKit 5.0 documents TimeoutException only for proxy connects; a socket timeout
        // normally surfaces as IOException.
        or TimeoutException;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(MimeKit.Text.TextFormat.Plain) { Text = message.Body };

        // Claim arithmetic (design 8, ADR 0017). The outbox starts a send only while at least SendBudget
        // (ClaimSeconds / 2) of the claim remains, and the deadline below cancels the whole send after
        // SendBudget, so the send always ends before the claim expires and no other instance can claim
        // and send the same row meanwhile. MailKit's SmtpClient.Timeout (milliseconds, applied to each
        // socket read and write) is SendBudget / 4: the four steps (connect, authenticate, send,
        // disconnect) each fail fast on a silent server, and a server that answers slowly but steadily
        // is stopped by the deadline. Default ClaimSeconds 120 -> budget 60 s, 15 s per socket operation;
        // minimum ClaimSeconds 30 -> 15 s and 3.75 s.
        var budget = settings.SendBudget;
        using var deadline = new CancellationTokenSource(budget, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        using var client = new SmtpClient { Timeout = (int)(budget.TotalMilliseconds / 4) };
        try
        {
            await client.ConnectAsync(
                settings.Host,
                settings.Port,
                settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                linked.Token);

            if (!string.IsNullOrWhiteSpace(settings.UserName))
            {
                await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty, linked.Token);
            }

            await client.SendAsync(mime, linked.Token);
            await client.DisconnectAsync(true, linked.Token);
        }
        catch (OperationCanceledException ex) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // An expected send failure (retried with backoff), not a shutdown.
            throw new IOException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The SMTP send did not finish within {budget.TotalSeconds} seconds."),
                ex);
        }
    }
}
