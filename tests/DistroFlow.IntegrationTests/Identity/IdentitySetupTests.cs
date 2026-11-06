using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Identity;

public class IdentitySetupTests : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly ApiIntegrationTestFactory _factory;

    public IdentitySetupTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Migrations_seed_every_employee_role()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var roleNames = await roleManager.Roles.Select(r => r.Name).ToListAsync();

        Assert.Equal(EmployeeRoles.All.Order(), roleNames.Order());
    }

    [Fact]
    public async Task UserManager_creates_user_with_hashed_password_and_assigns_role()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var tenant = new Tenant { Name = "Identity Setup Distributors" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            TenantId = tenant.Id,
            Email = $"owner-{Guid.NewGuid():N}@example.com"
        };
        user.UserName = user.Id.ToString();

        var created = await userManager.CreateAsync(user, "Str0ng!Passw0rd");
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

        var assigned = await userManager.AddToRoleAsync(user, EmployeeRoles.Administrator);
        Assert.True(assigned.Succeeded);

        Assert.NotEqual("Str0ng!Passw0rd", user.PasswordHash);
        Assert.True(await userManager.CheckPasswordAsync(user, "Str0ng!Passw0rd"));
        Assert.True(await userManager.IsInRoleAsync(user, EmployeeRoles.Administrator));
    }

    [Fact]
    public async Task Password_shorter_than_eight_characters_is_rejected()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var tenant = new Tenant { Name = "Weak Password Distributors" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var user = new ApplicationUser { TenantId = tenant.Id, Email = $"weak-{Guid.NewGuid():N}@example.com" };
        user.UserName = user.Id.ToString();

        var result = await userManager.CreateAsync(user, "Ab1!");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordTooShort");
    }
}
