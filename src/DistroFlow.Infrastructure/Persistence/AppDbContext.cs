
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DistroFlow.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Employee> Employees => Set<Employee>();

    private readonly ICurrentTenant _currentTenant;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Read by the tenant query filters on every query. Must be a member of the context
    /// (not a captured local) so EF evaluates it per query instead of baking in one value.
    /// Null for anonymous requests, which makes filtered queries return nothing.
    /// </summary>
    private Guid? CurrentTenantId => _currentTenant.TenantId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        // Tenant isolation: EF adds this WHERE to every Employee query automatically.
        // ApplicationUser is deliberately not filtered (see CLAUDE.md, Tenancy).
        builder.Entity<Employee>()
            .HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }
}
