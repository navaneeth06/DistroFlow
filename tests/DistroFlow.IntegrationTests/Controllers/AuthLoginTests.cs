using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Common;
using DistroFlow.Domain.Constants;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DistroFlow.IntegrationTests.Controllers;

public class AuthLoginTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string LoginUrl = "/api/v1/auth/login";
    private const string Password = "Str0ng!Passw0rd";
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AuthLoginTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_for_owner()
    {
        var owner = await RegisterAsync();

        var response = await LoginAsync(owner.Email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());
        Assert.True(data.GetProperty("expiresAtUtc").GetDateTime() > DateTime.UtcNow);

        var token = new JsonWebToken(data.GetProperty("accessToken").GetString());
        Assert.Equal(owner.UserId.ToString(), token.GetClaim(AppClaimTypes.UserId).Value);
        Assert.Equal(owner.EmployeeId.ToString(), token.GetClaim(AppClaimTypes.EmployeeId).Value);
        Assert.Equal(owner.TenantId.ToString(), token.GetClaim(AppClaimTypes.TenantId).Value);
        Assert.Equal(EmployeeRoles.Administrator, token.GetClaim(AppClaimTypes.Role).Value);
    }

    [Fact]
    public async Task Login_email_is_case_insensitive()
    {
        var owner = await RegisterAsync();

        var response = await LoginAsync(owner.Email.ToUpperInvariant(), Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_identical_401()
    {
        var owner = await RegisterAsync();

        var wrongPassword = await LoginAsync(owner.Email, "Wr0ng!Passw0rd");
        var unknownEmail = await LoginAsync(UniqueEmail(), Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal(InvalidCredentialsMessage, await MessageOf(wrongPassword));
    }

    [Fact]
    public async Task Five_failed_attempts_lock_the_account_even_for_correct_password()
    {
        var owner = await RegisterAsync();

        for (var i = 0; i < 5; i++)
        {
            await LoginAsync(owner.Email, "Wr0ng!Passw0rd");
        }

        var response = await LoginAsync(owner.Email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(InvalidCredentialsMessage, await MessageOf(response));
    }

    [Fact]
    public async Task Successful_login_resets_failed_attempt_count()
    {
        var owner = await RegisterAsync();

        for (var i = 0; i < 4; i++) await LoginAsync(owner.Email, "Wr0ng!Passw0rd");
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(owner.Email, Password)).StatusCode);
        for (var i = 0; i < 4; i++) await LoginAsync(owner.Email, "Wr0ng!Passw0rd");

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(owner.Email, Password)).StatusCode);
    }

    [Fact]
    public async Task Deactivated_employee_cannot_login()
    {
        var owner = await RegisterAsync();
        await DeactivateAsync(owner);

        var response = await LoginAsync(owner.Email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Suspended_distributor_returns_403()
    {
        var owner = await RegisterAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = await db.Tenants.SingleAsync(t => t.Id == owner.TenantId);
            tenant.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await LoginAsync(owner.Email, Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reused_email_logs_into_the_new_distributor_after_old_account_is_deactivated()
    {
        var email = UniqueEmail();
        var oldJob = await RegisterAsync(email);
        await DeactivateAsync(oldJob);
        var newJob = await RegisterAsync(email);

        var response = await LoginAsync(email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var token = new JsonWebToken(data.GetProperty("accessToken").GetString());
        Assert.Equal(newJob.TenantId.ToString(), token.GetClaim(AppClaimTypes.TenantId).Value);
        Assert.NotEqual(oldJob.TenantId, newJob.TenantId);
    }

    private sealed record RegisteredOwner(string Email, Guid TenantId, Guid EmployeeId, Guid UserId);

    private static string UniqueEmail() => $"login-{Guid.NewGuid():N}@example.com";

    private async Task<RegisteredOwner> RegisterAsync(string? email = null)
    {
        email ??= UniqueEmail();
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            businessName = $"Distributor {Guid.NewGuid():N}",
            timeZoneId = "Asia/Kolkata",
            ownerFullName = "Ravi Sharma",
            email,
            password = Password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return new RegisteredOwner(
            email,
            data.GetProperty("tenantId").GetGuid(),
            data.GetProperty("employeeId").GetGuid(),
            data.GetProperty("userId").GetGuid());
    }

    /// <summary>
    /// Deactivates both rows directly; the deactivate endpoint arrives with employee management.
    /// </summary>
    private async Task DeactivateAsync(RegisteredOwner owner)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.SingleAsync(u => u.Id == owner.UserId);
        // Test setup runs outside any request (no tenant), so bypass the tenant filter.
        var employee = await db.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == owner.EmployeeId);
        user.IsActive = false;
        employee.Deactivate(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync(LoginUrl, new { email, password });

    private static async Task<string?> MessageOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();
}
