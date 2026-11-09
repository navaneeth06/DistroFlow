using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Interfaces.Repositories;

public interface IEmployeeRepository
{
    void Add(Employee employee);

    /// <summary>
    /// Login only: finds the employee for an authenticated login across all tenants
    /// (no tenant is known before login). Do not use for tenant-facing queries.
    /// </summary>
    Task<Employee?> GetForLoginByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
