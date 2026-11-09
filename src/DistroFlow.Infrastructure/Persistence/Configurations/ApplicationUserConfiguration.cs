using DistroFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DistroFlow.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.TenantId);

        // An email may be reused after the person leaves a distributor (account deactivated),
        // but at any moment it belongs to at most one active account. Enforced by the database
        // so two distributors adding the same email at the same time cannot both succeed.
        // Overrides Identity's default non-unique "EmailIndex".
        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique()
            .HasFilter("\"IsActive\"");
    }
}
