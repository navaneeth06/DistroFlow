using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DistroFlow.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);

        // Lets future tables use composite (TenantId, EmployeeId) foreign keys,
        // so a row can never reference another tenant's employee.
        builder.HasAlternateKey(e => new { e.TenantId, e.Id });

        builder.Property(e => e.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(e => e.EmployeeCode)
            .HasMaxLength(20);

        builder.Property(e => e.Phone)
            .HasMaxLength(20);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();

        builder.Ignore(e => e.IsActive);

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deleting the login account (erasure) keeps the employee row for history.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Employee>(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");

        builder.HasIndex(e => new { e.TenantId, e.EmployeeCode })
            .IsUnique()
            .HasFilter("\"EmployeeCode\" IS NOT NULL");

        builder.HasIndex(e => new { e.TenantId, e.Status });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Employees_Status",
                "\"Status\" IN ('Active', 'Deactivated', 'Anonymised')");

            table.HasCheckConstraint(
                "CK_Employees_ActiveHasLogin",
                "\"Status\" <> 'Active' OR (\"UserId\" IS NOT NULL AND \"DeactivatedAtUtc\" IS NULL)");

            table.HasCheckConstraint(
                "CK_Employees_DeactivatedAt",
                "\"Status\" = 'Active' OR \"DeactivatedAtUtc\" IS NOT NULL");

            // Guarantees anonymised rows hold no personal data.
            table.HasCheckConstraint(
                "CK_Employees_AnonymisedHasNoPersonalData",
                "\"Status\" <> 'Anonymised' OR (\"AnonymisedAtUtc\" IS NOT NULL AND \"UserId\" IS NULL " +
                "AND \"Phone\" IS NULL AND \"EmployeeCode\" IS NULL AND \"FullName\" = 'Former employee')");
        });
    }
}
