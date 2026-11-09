using System.ComponentModel.DataAnnotations;

namespace DistroFlow.Application.DTOs.Employees;

/// <summary>
/// Full replacement of the editable profile fields (PUT): omitting phone or code clears it.
/// Email and roles are changed elsewhere (email is the login; roles have their own rules).
/// </summary>
public class UpdateEmployeeRequest
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^\+[1-9]\d{7,14}$",
        ErrorMessage = "Phone must be in E.164 format, e.g. +919876543210.")]
    public string? Phone { get; set; }

    [StringLength(20)]
    public string? EmployeeCode { get; set; }
}
