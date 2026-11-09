using System.ComponentModel.DataAnnotations;

namespace DistroFlow.Infrastructure.Email;

/// <summary>
/// Bound from the "Smtp" configuration section.
/// Development: Mailpit on localhost:1025 (no TLS, no login), view mail at http://localhost:8025.
/// Production: any SMTP provider; set Username/Password via user-secrets or env vars (Smtp__Password).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    /// <summary>Use STARTTLS/TLS. Off only for a local catcher such as Mailpit.</summary>
    public bool UseTls { get; init; } = true;

    public string? Username { get; init; }

    public string? Password { get; init; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    [Required]
    public string FromName { get; init; } = string.Empty;
}
