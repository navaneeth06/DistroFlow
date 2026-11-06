using DistroFlow.Application.DTOs.Tenants;

namespace DistroFlow.Application.Interfaces.Services;

public interface ITenantService
{
    Task<TenantResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<TenantResponse?> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}