
using DistroFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DistroFlow.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Email)
            .HasMaxLength(255);

        builder.Property(t => t.Phone)
            .HasMaxLength(20);

        builder.Property(t => t.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(t => t.InvoicePrefix)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.TimeZoneId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.IsActive)
    .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.Property(t => t.UpdatedAtUtc);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Tenants_FiscalYearStartMonth",
                "\"FiscalYearStartMonth\" BETWEEN 1 AND 12"));
    }
}
