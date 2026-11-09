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

    /// <summary>Changes name, phone and employee code of an employee of the current distributor.</summary>
    Task<UpdateEmployeeResult> UpdateAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Emails a fresh set-password link to an employee who has not accepted yet.</summary>
    Task<ResendInviteResult> ResendInviteAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);
}
