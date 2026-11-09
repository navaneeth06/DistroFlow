namespace DistroFlow.Application.Interfaces.Email;

/// <summary>
/// Sends an email. The Application layer only says *what* to send; how (SMTP, which
/// provider) is an Infrastructure and configuration decision.
/// </summary>
public interface IEmailSender
{
    /// <summary>Throws if the message could not be handed to the mail server.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <param name="TextBody">Plain-text version, for mail clients that do not show HTML.</param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody);
