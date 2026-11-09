using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Interfaces.Repositories;

public interface IEmployeeRepository
{
    void Add(Employee employee);

    /// <summary>Employees of the current tenant (tenant filter applies), ordered by name.</summary>
    Task<IReadOnlyList<Employee>> ListAsync(
        CancellationToken cancellationToken = default);

    /// <summary>An employee of the current tenant, or null (also null for another tenant's id).</summary>
    Task<Employee?> GetByIdReadOnlyAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Login only: finds the employee for an authenticated login across all tenants
    /// (no tenant is known before login). Do not use for tenant-facing queries.
    /// </summary>
    Task<Employee?> GetForLoginByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
