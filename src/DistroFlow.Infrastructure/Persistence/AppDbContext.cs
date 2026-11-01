
using DistroFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DistroFlow.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
