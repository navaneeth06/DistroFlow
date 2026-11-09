using System.Net;
using DistroFlow.Application.Interfaces.Email;

namespace DistroFlow.Application.Emails;

/// <summary>The email an invited employee receives, with the link to set their password.</summary>
public static class InvitationEmail
{
    public static EmailMessage Create(string to, string employeeName, string distributorName, string setPasswordLink)
    {
        // Names come from user input: encode them so they can never become HTML/script in the email.
        var name = WebUtility.HtmlEncode(employeeName);
        var distributor = WebUtility.HtmlEncode(distributorName);
        var link = WebUtility.HtmlEncode(setPasswordLink);

        var subject = $"You're invited to {distributorName} on DistroFlow";

        var html = $"""
            <p>Hi {name},</p>
            <p>You've been added to <strong>{distributor}</strong> on DistroFlow.</p>
            <p><a href="{link}">Set your password</a> to get started. This link can be used once and expires in 3 days.</p>
            <p>If you weren't expecting this, you can ignore this email.</p>
            """;

        var text = $"""
            Hi {employeeName},

            You've been added to {distributorName} on DistroFlow.
            Set your password to get started (the link can be used once and expires in 3 days):
            {setPasswordLink}

            If you weren't expecting this, you can ignore this email.
            """;

        return new EmailMessage(to, subject, html, text);
    }
}
