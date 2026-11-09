using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Controllers;

public class AuthControllerTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string RegisterUrl = "/api/v1/auth/register";
    private const string ValidPassword = "Str0ng!Passw0rd";

    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_creates_tenant_owner_login_and_administrator_employee()
    {
        var email = UniqueEmail();
        var request = ValidRequest(email, businessName: "Sharma Traders");

        var response = await _client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var tenantId = data.GetProperty("tenantId").GetGuid();
        var employeeId = data.GetProperty("employeeId").GetGuid();
        var userId = data.GetProperty("userId").GetGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal("Sharma Traders", tenant.Name);
        Assert.Equal("Asia/Kolkata", tenant.TimeZoneId);

        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal(email, user.Email);
        Assert.True(await userManager.CheckPasswordAsync(user, ValidPassword));
        Assert.True(await userManager.IsInRoleAsync(user, EmployeeRoles.Administrator));

        // Verifying the database directly, outside any request (no tenant): bypass the tenant filter.
        var employee = await db.Employees.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == employeeId);
        Assert.Equal(tenantId, employee.TenantId);
        Assert.Equal(userId, employee.UserId);
        Assert.Equal("Ravi Sharma", employee.FullName);
        Assert.Equal("+919876543210", employee.Phone);
        Assert.Equal(EmployeeStatus.Active, employee.Status);
    }

    [Fact]
    public async Task Register_with_email_active_elsewhere_returns_409_and_saves_nothing()
    {
        var email = UniqueEmail();
        var first = await _client.PostAsJsonAsync(RegisterUrl, ValidRequest(email));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var secondBusiness = $"Second {Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync(RegisterUrl, ValidRequest(email, secondBusiness));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());

        // The tenant row was inserted before the login failed; the transaction must have rolled it back.
        Assert.False(await TenantExistsAsync(secondBusiness));
    }

    [Fact]
    public async Task Register_with_password_failing_identity_policy_returns_400_and_saves_nothing()
    {
        var businessName = $"Weak {Guid.NewGuid():N}";
        // Long enough for the DTO, but no uppercase/digit/symbol: rejected by Identity.
        var request = ValidRequest(UniqueEmail(), businessName) with { password = "weakpassword" };

        var response = await _client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.GetProperty("Password").GetArrayLength() > 0);
        Assert.False(await TenantExistsAsync(businessName));
    }

    [Fact]
    public async Task Register_with_invalid_body_returns_400_and_saves_nothing()
    {
        var businessName = $"Invalid {Guid.NewGuid():N}";
        var request = ValidRequest("not-an-email", businessName) with { ownerPhone = "98765" };

        var response = await _client.PostAsJsonAsync(RegisterUrl, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("OwnerPhone", out _));
        Assert.False(await TenantExistsAsync(businessName));
    }

    private static string UniqueEmail() => $"owner-{Guid.NewGuid():N}@example.com";

    private static RegisterBody ValidRequest(string email, string? businessName = null) => new(
        businessName: businessName ?? $"Distributor {Guid.NewGuid():N}",
        timeZoneId: "Asia/Kolkata",
        ownerFullName: "Ravi Sharma",
        ownerPhone: "+919876543210",
        email: email,
        password: ValidPassword);

    private async Task<bool> TenantExistsAsync(string name)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.AnyAsync(t => t.Name == name);
    }

    // Lower-case names so the JSON matches what the frontend will send.
    private sealed record RegisterBody(
        string businessName,
        string timeZoneId,
        string ownerFullName,
        string? ownerPhone,
        string email,
        string password);
}
