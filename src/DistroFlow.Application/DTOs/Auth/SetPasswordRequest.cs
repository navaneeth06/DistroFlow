using System.ComponentModel.DataAnnotations;

namespace DistroFlow.Application.DTOs.Auth;

/// <summary>Sent by the frontend's set-password page, with userId and token taken from the invite link.</summary>
public class SetPasswordRequest
{
    [Required]
    public Guid? UserId { get; set; }

    [Required]
    [StringLength(2048)]
    public string Token { get; set; } = string.Empty;

    /// <summary>Strength rules are enforced by Identity's password policy.</summary>
    [Required]
    [StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
