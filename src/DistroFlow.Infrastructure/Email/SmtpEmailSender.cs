using DistroFlow.Application.Interfaces.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DistroFlow.Infrastructure.Email;

/// <summary>Sends email over SMTP with MailKit (works with Mailpit locally and any SMTP provider).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        // A new connection per email keeps this simple; fine for low volume (invites).
        using var client = new SmtpClient();

        await client.ConnectAsync(_options.Host, _options.Port, SocketOptions(), cancellationToken);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }

    /// <summary>
    /// TLS is required when enabled (never silently downgraded to plain text, which would
    /// expose the SMTP password). Port 465 uses TLS from the start; others upgrade via STARTTLS.
    /// </summary>
    private SecureSocketOptions SocketOptions()
    {
        if (!_options.UseTls)
            return SecureSocketOptions.None;

        return _options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }
}
