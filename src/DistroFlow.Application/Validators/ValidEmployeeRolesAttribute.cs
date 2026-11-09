using System.ComponentModel.DataAnnotations;
using DistroFlow.Domain.Constants;

namespace DistroFlow.Application.Validators;

/// <summary>At least one role, no duplicates, and each one of <see cref="EmployeeRoles.All"/>.</summary>
public class ValidEmployeeRolesAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IEnumerable<string> roles || !roles.Any())
        {
            return new ValidationResult("At least one role is required.");
        }

        var unknown = roles.Where(r => !EmployeeRoles.All.Contains(r)).ToList();
        if (unknown.Count > 0)
        {
            return new ValidationResult(
                $"Unknown role(s): {string.Join(", ", unknown)}. Allowed: {string.Join(", ", EmployeeRoles.All)}.");
        }

        if (roles.Distinct().Count() != roles.Count())
        {
            return new ValidationResult("Roles must not contain duplicates.");
        }

        return ValidationResult.Success;
    }
}
