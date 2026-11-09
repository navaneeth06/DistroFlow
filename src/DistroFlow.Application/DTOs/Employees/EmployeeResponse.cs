namespace DistroFlow.Application.DTOs.Employees;

public class EmployeeResponse
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Login email. Null once the employee is anonymised (login deleted).</summary>
    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? EmployeeCode { get; set; }

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
