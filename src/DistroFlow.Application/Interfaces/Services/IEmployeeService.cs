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

    /// <summary>
    /// Replaces an employee's roles. Refuses to leave the distributor without an active
    /// Administrator, also when two such changes run at the same time.
    /// </summary>
    Task<ChangeEmployeeRolesResult> ChangeRolesAsync(
        Guid employeeId,
        ChangeEmployeeRolesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The employee left or is suspended: they can no longer log in and their email becomes
    /// reusable. Refuses to deactivate the last active Administrator.
    /// </summary>
    Task<EmployeeStatusChangeResult> DeactivateAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    /// <summary>Emails a fresh set-password link to an employee who has not accepted yet.</summary>
    Task<ResendInviteResult> ResendInviteAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);
}
