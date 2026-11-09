using DistroFlow.Application.DTOs.Tenants;

namespace DistroFlow.Application.Interfaces.Services;

/// <summary>
/// The caller's own distributor. The tenant always comes from <see cref="Identity.ICurrentTenant"/>
/// (the JWT), never from a route or body, so one distributor can never address another.
/// </summary>
public interface ITenantService
{
    Task<TenantResponse?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<TenantResponse?> UpdateCurrentAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);
}
