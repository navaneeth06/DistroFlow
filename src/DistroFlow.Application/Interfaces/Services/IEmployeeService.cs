using DistroFlow.Application.DTOs.Employees;

namespace DistroFlow.Application.Interfaces.Services;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeResponse>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<EmployeeResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
