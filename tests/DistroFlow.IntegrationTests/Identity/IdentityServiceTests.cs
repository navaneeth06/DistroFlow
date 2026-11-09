using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Identity;

public class IdentityServiceTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string ValidPassword = "Str0ng!Passw0rd";

    private readonly ApiIntegrationTestFactory _factory;

    public IdentityServiceTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateUser_creates_active_login_with_hashed_password_and_role()
    {
        var tenant = await CreateTenantAsync();
        var email = UniqueEmail();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await service.CreateUserAsync(tenant.Id, email, ValidPassword, EmployeeRoles.Administrator);

        Assert.True(result.Succeeded);
        var user = await userManager.FindByIdAsync(result.UserId!.Value.ToString());
        Assert.NotNull(user);
        Assert.Equal(tenant.Id, user.TenantId);
        Assert.Equal(email, user.Email);
        Assert.True(user.IsActive);
        Assert.True(await userManager.CheckPasswordAsync(user, ValidPassword));
        Assert.True(await userManager.IsInRoleAsync(user, EmployeeRoles.Administrator));
    }

    [Fact]
    public async Task CreateUser_returns_EmailInUse_when_email_is_active_in_another_tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var email = UniqueEmail();

        await using (var first = _factory.Services.CreateAsyncScope())
        {
            var service = first.ServiceProvider.GetRequiredService<IIdentityService>();
            Assert.True((await service.CreateUserAsync(tenantA.Id, email, ValidPassword, EmployeeRoles.SalesStaff)).Succeeded);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var secondService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Different casing must still count as the same email.
        var result = await secondService.CreateUserAsync(
            tenantB.Id, email.ToUpperInvariant(), ValidPassword, EmployeeRoles.SalesStaff);

        Assert.False(result.Succeeded);
        Assert.Equal(CreateUserError.EmailInUse, result.Error);
        Assert.Null(result.UserId);

        // The failed insert must not stay tracked and be retried by a later save.
        Assert.DoesNotContain(db.ChangeTracker.Entries<ApplicationUser>(), e => e.State == EntityState.Added);
        await db.SaveChangesAsync();
        Assert.Equal(0, await db.Users.CountAsync(u => u.TenantId == tenantB.Id));
    }

    [Fact]
    public async Task CreateUser_returns_ValidationFailed_for_weak_password_and_saves_nothing()
    {
        var tenant = await CreateTenantAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await service.CreateUserAsync(tenant.Id, UniqueEmail(), "short", EmployeeRoles.SalesStaff);

        Assert.False(result.Succeeded);
        Assert.Equal(CreateUserError.ValidationFailed, result.Error);
        Assert.NotEmpty(result.Messages);
        Assert.Equal(0, await db.Users.CountAsync(u => u.TenantId == tenant.Id));
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<Tenant> CreateTenantAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant { Name = $"Distributor {Guid.NewGuid():N}" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }
}
