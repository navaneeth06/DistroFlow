using System.ComponentModel.DataAnnotations;
using DistroFlow.Application.Validators;

namespace DistroFlow.Application.DTOs.Tenants;

public class CreateTenantRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }

    [Phone]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Required]
    [RegularExpression(@"^[A-Z]{3}$",
        ErrorMessage = "Currency code must contain exactly 3 uppercase letters.")]
    public string CurrencyCode { get; set; } = "INR";

    [Required]
    [StringLength(10, MinimumLength = 2)]
    public string InvoicePrefix { get; set; } = "INV";

    [Range(1, 12)]
    public int FiscalYearStartMonth { get; set; } = 4;

    [Required]
    [StringLength(64)]
    [ValidTimeZone]
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
}