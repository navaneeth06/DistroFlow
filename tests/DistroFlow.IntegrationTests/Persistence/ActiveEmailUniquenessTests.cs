using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DistroFlow.IntegrationTests.Persistence;

/// <summary>
/// Proves the database (not just application code) enforces:
/// an email belongs to at most one active login account at a time.
/// </summary>
public class ActiveEmailUniquenessTests : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly ApiIntegrationTestFactory _factory;

    public ActiveEmailUniquenessTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Two_active_accounts_with_same_email_in_different_tenants_are_rejected()
    {
        var email = UniqueEmail();
        var tenantA = await CreateTenantAsync("Distributor A");
        var tenantB = await CreateTenantAsync("Distributor B");

        await AddUserAsync(tenantA.Id, email, isActive: true);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => AddUserAsync(tenantB.Id, email, isActive: true));

        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
        Assert.Equal("EmailIndex", pg.ConstraintName);
    }

    [Fact]
    public async Task Email_can_be_reused_once_previous_account_is_deactivated()
    {
        var email = UniqueEmail();
        var oldEmployer = await CreateTenantAsync("Old Employer");
        var newEmployer = await CreateTenantAsync("New Employer");

        await AddUserAsync(oldEmployer.Id, email, isActive: false);

        await AddUserAsync(newEmployer.Id, email, isActive: true);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Reactivating_old_account_fails_while_email_is_active_elsewhere()
    {
        var email = UniqueEmail();
        var oldEmployer = await CreateTenantAsync("Old Employer");
        var newEmployer = await CreateTenantAsync("New Employer");

        var oldAccount = await AddUserAsync(oldEmployer.Id, email, isActive: false);
        await AddUserAsync(newEmployer.Id, email, isActive: true);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == oldAccount.Id);
        user.IsActive = true;

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<Tenant> CreateTenantAsync(string name)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant { Name = name };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private async Task<ApplicationUser> AddUserAsync(Guid tenantId, string email, bool isActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new ApplicationUser
        {
            TenantId = tenantId,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            IsActive = isActive
        };
        user.UserName = user.Id.ToString();
        user.NormalizedUserName = user.UserName.ToUpperInvariant();

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
