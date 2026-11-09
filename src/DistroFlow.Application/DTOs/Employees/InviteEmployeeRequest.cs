using System.ComponentModel.DataAnnotations;
using DistroFlow.Application.Validators;

namespace DistroFlow.Application.DTOs.Employees;

/// <summary>
/// An Administrator adds an employee. No password: the employee sets it via the emailed link.
/// </summary>
public class InviteEmployeeRequest
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^\+[1-9]\d{7,14}$",
        ErrorMessage = "Phone must be in E.164 format, e.g. +919876543210.")]
    public string? Phone { get; set; }

    [StringLength(20)]
    public string? EmployeeCode { get; set; }

    [ValidEmployeeRoles]
    public List<string> Roles { get; set; } = [];
}
