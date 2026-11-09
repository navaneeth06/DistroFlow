using System.ComponentModel.DataAnnotations;
using DistroFlow.Application.Validators;

namespace DistroFlow.Application.DTOs.Auth;

/// <summary>
/// Self-signup: creates a distributor (tenant) and its first Administrator.
/// Currency, invoice prefix and fiscal year use defaults and can be edited later.
/// </summary>
public class RegisterDistributorRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string BusinessName { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    [ValidTimeZone]
    public string TimeZoneId { get; set; } = "Asia/Kolkata";

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string OwnerFullName { get; set; } = string.Empty;

    [RegularExpression(@"^\+[1-9]\d{7,14}$",
        ErrorMessage = "Phone must be in E.164 format, e.g. +919876543210.")]
    public string? OwnerPhone { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Strength rules are enforced by Identity's password policy.</summary>
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}
