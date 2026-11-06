using DistroFlow.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DistroFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Seeds the fixed employee roles as reference data in a migration.
/// Ids and concurrency stamps are constants so the migration is deterministic
/// (EF would otherwise generate a new migration every time).
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.HasData(
            Role("c2281575-f1a9-41f8-810d-f53174c2b4d1", EmployeeRoles.Administrator),
            Role("b4f5e138-6741-45bb-a395-c971dc8e7962", EmployeeRoles.SalesStaff),
            Role("a3a51997-11f6-480c-8c9d-28210ab7e385", EmployeeRoles.DispatchStaff),
            Role("42e5faac-8ca2-41e8-98bc-f842f205e9aa", EmployeeRoles.Approver),
            Role("295f85df-e212-4cbc-8ee1-48b3c6ec2fee", EmployeeRoles.AccountsStaff));
    }

    private static IdentityRole<Guid> Role(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id
    };
}
