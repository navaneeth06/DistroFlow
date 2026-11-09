using DistroFlow.Application.DTOs.Tenants;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;

    public TenantService(ITenantRepository tenantRepository, ICurrentTenant currentTenant)
    {
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
    }

    public async Task<TenantResponse?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdReadOnlyAsync(
            CurrentTenantId(), cancellationToken);

        return tenant is null ? null : MapToResponse(tenant);
    }

    public async Task<TenantResponse?> UpdateCurrentAsync(
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(
            CurrentTenantId(), cancellationToken);

        if (tenant is null)
            return null;

        tenant.Name = request.Name.Trim();
        tenant.Email = request.Email?.Trim();
        tenant.Phone = request.Phone?.Trim();
        tenant.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        tenant.InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant();
        tenant.FiscalYearStartMonth = request.FiscalYearStartMonth;
        tenant.TimeZoneId = request.TimeZoneId.Trim();
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _tenantRepository.SaveChangesAsync(cancellationToken);

        return MapToResponse(tenant);
    }

    /// <summary>
    /// These endpoints require authentication, so a tenant is always present.
    /// A missing one means an endpoint was exposed anonymously by mistake: fail loudly.
    /// </summary>
    private Guid CurrentTenantId() =>
        _currentTenant.TenantId
        ?? throw new InvalidOperationException(
            "No current tenant. Tenant endpoints must require an authenticated user.");

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
