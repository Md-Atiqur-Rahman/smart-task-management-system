using System.Net.Sockets;

using Datavanced.SmartTaskManagement.Application.Abstractions.Email;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Email;

using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace Datavanced.SmartTaskManagement.Infrastructure.Email;

/// <summary>
/// Sends one plain-text email over SMTP with MailKit 5.0: connect (STARTTLS when Email:UseTls),
/// authenticate when Email:UserName is set, send, disconnect. The server certificate is always
/// validated by MailKit's default callback; it is never replaced.
/// </summary>
public sealed class MailKitEmailSender(IOptions<EmailOptions> options) : IEmailSender
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
        or IOException;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;
        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(MimeKit.Text.TextFormat.Plain) { Text = message.Body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            ct);

        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty, ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }
}
