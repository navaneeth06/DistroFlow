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

    [Fact]
    public async Task CreateInvitedUser_creates_login_without_password_and_with_all_roles()
    {
        var tenant = await CreateTenantAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await service.CreateInvitedUserAsync(
            tenant.Id, UniqueEmail(), [EmployeeRoles.SalesStaff, EmployeeRoles.Approver]);

        Assert.True(result.Succeeded);
        var user = await userManager.FindByIdAsync(result.UserId!.Value.ToString());
        Assert.NotNull(user);
        Assert.False(await userManager.HasPasswordAsync(user));
        Assert.Equal(
            [EmployeeRoles.Approver, EmployeeRoles.SalesStaff],
            (await userManager.GetRolesAsync(user)).Order());
    }

    [Fact]
    public async Task Password_setup_token_is_accepted_once_and_then_stops_working()
    {
        var tenant = await CreateTenantAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await service.CreateInvitedUserAsync(tenant.Id, UniqueEmail(), [EmployeeRoles.SalesStaff]);
        var token = await service.GeneratePasswordSetupTokenAsync(created.UserId!.Value);
        var user = (await userManager.FindByIdAsync(created.UserId.Value.ToString()))!;

        var first = await userManager.ResetPasswordAsync(user, token, ValidPassword);
        var reused = await userManager.ResetPasswordAsync(user, token, "An0ther!Passw0rd");

        Assert.True(first.Succeeded);
        Assert.False(reused.Succeeded); // single use: setting the password invalidates the token
        Assert.True(await userManager.CheckPasswordAsync(user, ValidPassword));
    }

    [Fact]
    public async Task ReplaceRoles_adds_missing_and_removes_unlisted_roles()
    {
        var tenant = await CreateTenantAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await service.CreateInvitedUserAsync(
            tenant.Id, UniqueEmail(), [EmployeeRoles.SalesStaff, EmployeeRoles.Approver]);

        await service.ReplaceRolesAsync(created.UserId!.Value, [EmployeeRoles.Approver, EmployeeRoles.DispatchStaff]);

        var user = (await userManager.FindByIdAsync(created.UserId.Value.ToString()))!;
        Assert.Equal(
            [EmployeeRoles.Approver, EmployeeRoles.DispatchStaff],
            (await userManager.GetRolesAsync(user)).Order());
    }

    [Fact]
    public async Task CountActiveAdministrators_counts_only_active_admins_of_that_tenant()
    {
        var tenant = await CreateTenantAsync();
        var otherTenant = await CreateTenantAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await service.CreateInvitedUserAsync(tenant.Id, UniqueEmail(), [EmployeeRoles.Administrator]);          // counted
        await service.CreateInvitedUserAsync(tenant.Id, UniqueEmail(), [EmployeeRoles.SalesStaff]);             // not an admin
        await service.CreateInvitedUserAsync(otherTenant.Id, UniqueEmail(), [EmployeeRoles.Administrator]);     // other tenant
        var inactive = await service.CreateInvitedUserAsync(tenant.Id, UniqueEmail(), [EmployeeRoles.Administrator]);
        (await db.Users.SingleAsync(u => u.Id == inactive.UserId)).IsActive = false;                          // inactive
        await db.SaveChangesAsync();

        Assert.Equal(1, await service.CountActiveAdministratorsAsync(tenant.Id));
    }

    [Fact]
    public async Task Deactivating_a_login_also_invalidates_its_invite_link()
    {
        var tenant = await CreateTenantAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await service.CreateInvitedUserAsync(tenant.Id, UniqueEmail(), [EmployeeRoles.SalesStaff]);
        var token = await service.GeneratePasswordSetupTokenAsync(created.UserId!.Value);

        var result = await service.SetLoginActiveAsync(created.UserId.Value, isActive: false);

        Assert.Equal(SetLoginActiveResult.Succeeded, result);
        var user = (await userManager.FindByIdAsync(created.UserId.Value.ToString()))!;
        Assert.False(user.IsActive);
        Assert.False(await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose,
            token));
    }

    [Fact]
    public async Task Reactivating_a_login_succeeds_when_the_email_is_free()
    {
        var tenant = await CreateTenantAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var created = await service.CreateInvitedUserAsync(tenant.Id, UniqueEmail(), [EmployeeRoles.SalesStaff]);
        await service.SetLoginActiveAsync(created.UserId!.Value, isActive: false);

        var result = await service.SetLoginActiveAsync(created.UserId.Value, isActive: true);

        Assert.Equal(SetLoginActiveResult.Succeeded, result);
        Assert.True((await db.Users.AsNoTracking().SingleAsync(u => u.Id == created.UserId)).IsActive);
    }

    [Fact]
    public async Task Reactivating_a_login_whose_email_is_active_elsewhere_returns_EmailInUse()
    {
        var oldEmployer = await CreateTenantAsync();
        var newEmployer = await CreateTenantAsync();
        var email = UniqueEmail();

        Guid oldLogin;
        await using (var setup = _factory.Services.CreateAsyncScope())
        {
            var setupService = setup.ServiceProvider.GetRequiredService<IIdentityService>();
            oldLogin = (await setupService.CreateInvitedUserAsync(oldEmployer.Id, email, [EmployeeRoles.SalesStaff])).UserId!.Value;
            await setupService.SetLoginActiveAsync(oldLogin, isActive: false);   // left the old job
            Assert.True((await setupService.CreateInvitedUserAsync(newEmployer.Id, email, [EmployeeRoles.SalesStaff])).Succeeded);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await service.SetLoginActiveAsync(oldLogin, isActive: true);

        Assert.Equal(SetLoginActiveResult.EmailInUse, result);
        Assert.False((await db.Users.AsNoTracking().SingleAsync(u => u.Id == oldLogin)).IsActive);
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
