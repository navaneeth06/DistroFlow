using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Interfaces.Repositories;

public interface ITenantRepository
{
    void Add(Tenant tenant);

    Task<Tenant?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Tenant?> GetByIdReadOnlyAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks the tenant's row until the current transaction ends (SELECT ... FOR UPDATE).
    /// Other transactions that lock the same tenant wait, so changes that must see each
    /// other's results (e.g. "at least one administrator") run one at a time per tenant.
    /// Must be called inside a transaction.
    /// </summary>
    Task LockForUpdateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}