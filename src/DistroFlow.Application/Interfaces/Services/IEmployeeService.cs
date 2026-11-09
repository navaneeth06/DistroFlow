using DistroFlow.Application.DTOs.Employees;

namespace DistroFlow.Application.Interfaces.Services;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeResponse>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<EmployeeResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an employee to the current distributor and emails them a set-password link.
    /// </summary>
    Task<InviteEmployeeResult> InviteAsync(
        InviteEmployeeRequest request,
        CancellationToken cancellationToken = default);
}
