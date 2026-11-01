namespace DistroFlow.Application.DTOs.Tenants;

public class TenantResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string InvoicePrefix { get; set; } = string.Empty;

    public int FiscalYearStartMonth { get; set; }

    public string TimeZoneId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}