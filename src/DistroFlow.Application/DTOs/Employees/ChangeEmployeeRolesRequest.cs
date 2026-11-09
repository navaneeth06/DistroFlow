using DistroFlow.Application.Validators;

namespace DistroFlow.Application.DTOs.Employees;

/// <summary>The employee's complete new set of roles (replaces the current ones).</summary>
public class ChangeEmployeeRolesRequest
{
    [ValidEmployeeRoles]
    public List<string> Roles { get; set; } = [];
}
