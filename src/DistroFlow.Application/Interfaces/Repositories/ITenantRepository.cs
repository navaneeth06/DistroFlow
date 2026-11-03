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

    Task<IReadOnlyList<Tenant>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}