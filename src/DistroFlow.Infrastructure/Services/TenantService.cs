using DistroFlow.Application.DTOs.Tenants;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DistroFlow.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly AppDbContext _dbContext;

    public TenantService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantResponse> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            InvoicePrefix = request.InvoicePrefix.Trim().ToUpperInvariant(),
            FiscalYearStartMonth = request.FiscalYearStartMonth,
            TimeZoneId = request.TimeZoneId.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return tenant is null ? null : MapToResponse(tenant);
    }

    public async Task<IReadOnlyList<TenantResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await _dbContext.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tenants.Select(MapToResponse).ToList();
    }

    public async Task<TenantResponse?> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

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

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(tenant);
    }

    public async Task<bool> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tenant is null)
            return false;

        if (!tenant.IsActive)
            return true;

        tenant.IsActive = false;
        tenant.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

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