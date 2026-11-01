namespace DistroFlow.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string CurrencyCode { get; set; } = "INR";

    public string InvoicePrefix { get; set; } = "INV";

    public int FiscalYearStartMonth { get; set; } = 4;

    public string TimeZoneId { get; set; } = "Asia/Kolkata";
}