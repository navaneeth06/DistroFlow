
using DistroFlow.Application.DTOs.Tenants;
using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;

    public TenantService(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<TenantResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdReadOnlyAsync(
            id, cancellationToken);

        return tenant is null ? null : MapToResponse(tenant);
    }

    public async Task<IReadOnlyList<TenantResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await _tenantRepository.GetAllAsync(cancellationToken);

        return tenants.Select(MapToResponse).ToList();
    }

    public async Task<TenantResponse?> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(
            id, cancellationToken);

        if (tenant is null)
            return null;

        tenant.Name = request.Name.Trim();
        tenant.Email = request.Email?.Trim();
        tenant.Phone = request.Phone?.Trim();
        tenant.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        tenant.InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant();
        tenant.FiscalYearStartMonth = request.FiscalYearStartMonth;
        tenant.TimeZoneId = request.TimeZoneId.Trim();
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _tenantRepository.SaveChangesAsync(cancellationToken);

        return MapToResponse(tenant);
    }

    public async Task<bool> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(
            id, cancellationToken);

        if (tenant is null)
            return false;

        if (!tenant.IsActive)
            return true;

        tenant.IsActive = false;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _tenantRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static TenantResponse MapToResponse(Tenant tenant)
    {
        return new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Email = tenant.Email,
            Phone = tenant.Phone,
            CurrencyCode = tenant.CurrencyCode,
            InvoicePrefix = tenant.InvoicePrefix,
            FiscalYearStartMonth = tenant.FiscalYearStartMonth,
            TimeZoneId = tenant.TimeZoneId,
            IsActive = tenant.IsActive,
            CreatedAtUtc = tenant.CreatedAtUtc,
            UpdatedAtUtc = tenant.UpdatedAtUtc
        };
    }
}
