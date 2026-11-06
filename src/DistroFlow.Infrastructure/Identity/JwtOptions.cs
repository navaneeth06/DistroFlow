using System.ComponentModel.DataAnnotations;

namespace DistroFlow.Infrastructure.Identity;

/// <summary>
/// Bound from the "Jwt" configuration section. Issuer/audience/lifetime live in appsettings.json;
/// the signing key is a secret and comes from user-secrets (dev) or environment variables (prod).
/// Validated at startup so a missing or weak key fails fast instead of on the first login.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 key; at least 32 characters (256 bits).</summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Kept short: a JWT stays valid until it expires, even if the employee is deactivated.</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 30;
}
