using Microsoft.AspNetCore.Identity;
using DistroFlow.Domain.Entities;

namespace DistroFlow.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    /// <summary>
    /// Mirrors <see cref="Employee.Status"/> == Active. Kept on the login row so the database
    /// can enforce "an email belongs to at most one active account" with a partial unique index
    /// (an index cannot look at the Employees table). Updated in the same transaction as the employee.
    /// </summary>
    public bool IsActive { get; set; } = true;
}