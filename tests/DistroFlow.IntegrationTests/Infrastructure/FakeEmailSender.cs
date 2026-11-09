using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DistroFlow.Application.Interfaces.Email;

namespace DistroFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Records emails instead of sending them, so API tests need no mail server.
/// Any recipient containing <see cref="FailingRecipientMarker"/> simulates a mail server error.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    public const string FailingRecipientMarker = "mail-server-down";

    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (message.To.Contains(FailingRecipientMarker, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Simulated SMTP failure.");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The single email sent to this address (tests share one sender, so filter by recipient).</summary>
    public EmailMessage SingleTo(string recipient) =>
        Assert.Single(_sent, m => string.Equals(m.To, recipient, StringComparison.OrdinalIgnoreCase));

    /// <summary>userId and token from the set-password link in the latest email to this address.</summary>
    public (Guid UserId, string Token) SetPasswordLinkTo(string recipient)
    {
        var latest = _sent.Last(m => string.Equals(m.To, recipient, StringComparison.OrdinalIgnoreCase));
        return ParseSetPasswordLink(latest.TextBody);
    }

    public static (Guid UserId, string Token) ParseSetPasswordLink(string text)
    {
        var match = Regex.Match(text, @"/set-password\?userId=(?<userId>[^&\s]+)&token=(?<token>\S+)");
        Assert.True(match.Success, "Email does not contain a set-password link.");
        return (Guid.Parse(match.Groups["userId"].Value), Uri.UnescapeDataString(match.Groups["token"].Value));
    }
}
