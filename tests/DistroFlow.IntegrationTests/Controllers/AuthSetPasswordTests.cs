using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Common;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DistroFlow.IntegrationTests.Controllers;

/// <summary>The full invite journey: invited → email link → set password → log in.</summary>
public class AuthSetPasswordTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string SetPasswordUrl = "/api/v1/auth/set-password";
    private const string NewPassword = "Inv1ted!Passw0rd";
    private const string InvalidLinkMessage = "This link is invalid or has expired. Ask your administrator to resend the invite.";

    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _anonymous;

    public AuthSetPasswordTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task Invited_employee_sets_password_from_email_link_and_can_log_in()
    {
        var admin = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var invitee = await InviteAsync(admin, EmployeeRoles.DispatchStaff);
        var (userId, token) = _factory.Emails.SetPasswordLinkTo(invitee.Email);

        var response = await SetPasswordAsync(userId, token, NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = invitee.Email, password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var accessToken = new JsonWebToken((await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("accessToken").GetString());
        Assert.Equal(admin.TenantId.ToString(), accessToken.GetClaim(AppClaimTypes.TenantId).Value);
        Assert.Equal(invitee.EmployeeId.ToString(), accessToken.GetClaim(AppClaimTypes.EmployeeId).Value);
        Assert.Equal(EmployeeRoles.DispatchStaff, accessToken.GetClaim(AppClaimTypes.Role).Value);
    }

    [Fact]
    public async Task Setting_the_password_confirms_the_email()
    {
        var admin = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var invitee = await InviteAsync(admin, EmployeeRoles.SalesStaff);
        var (userId, token) = _factory.Emails.SetPasswordLinkTo(invitee.Email);

        await SetPasswordAsync(userId, token, NewPassword);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await db.Users.SingleAsync(u => u.Id == userId)).EmailConfirmed);
    }

    [Fact]
    public async Task Link_works_only_once()
    {
        var admin = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var invitee = await InviteAsync(admin, EmployeeRoles.SalesStaff);
        var (userId, token) = _factory.Emails.SetPasswordLinkTo(invitee.Email);

        var first = await SetPasswordAsync(userId, token, NewPassword);
        var second = await SetPasswordAsync(userId, token, "Sec0nd!Passw0rd");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(InvalidLinkMessage, await MessageOf(second));
    }

    [Fact]
    public async Task Tampered_token_and_unknown_user_give_the_same_invalid_link_response()
    {
        var admin = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var invitee = await InviteAsync(admin, EmployeeRoles.SalesStaff);
        var (userId, token) = _factory.Emails.SetPasswordLinkTo(invitee.Email);

        var tampered = await SetPasswordAsync(userId, token[..^4] + "AAAA", NewPassword);
        var unknownUser = await SetPasswordAsync(Guid.CreateVersion7(), token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, tampered.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownUser.StatusCode);
        Assert.Equal(await tampered.Content.ReadAsStringAsync(), await unknownUser.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Weak_password_is_rejected_and_the_link_stays_usable()
    {
        var admin = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var invitee = await InviteAsync(admin, EmployeeRoles.SalesStaff);
        var (userId, token) = _factory.Emails.SetPasswordLinkTo(invitee.Email);

        var weak = await SetPasswordAsync(userId, token, "weakpassword");
        var retry = await SetPasswordAsync(userId, token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var errors = (await weak.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.GetProperty("NewPassword").GetArrayLength() > 0);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task Cannot_be_used_to_reset_the_password_of_an_account_that_already_has_one()
    {
        // The owner registered with a password. Even with a valid token, this endpoint must refuse.
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
        string token;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            token = await scope.ServiceProvider.GetRequiredService<IIdentityService>()
                .GeneratePasswordSetupTokenAsync(owner.UserId);
        }

        var response = await SetPasswordAsync(owner.UserId, token, "Hijack3d!Passw0rd");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var login = await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = owner.Email, password = TestAuth.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode); // original password still works
    }

    [Fact]
    public async Task Deactivated_employee_cannot_use_their_invite_link()
    {
        var admin = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var invitee = await InviteAsync(admin, EmployeeRoles.SalesStaff);
        var (userId, token) = _factory.Emails.SetPasswordLinkTo(invitee.Email);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var employee = await db.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == invitee.EmployeeId);
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            employee.Deactivate(DateTime.UtcNow);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await SetPasswordAsync(userId, token, NewPassword);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(InvalidLinkMessage, await MessageOf(response));
    }

    private sealed record Invitee(Guid EmployeeId, string Email);

    private async Task<Invitee> InviteAsync(RegisteredDistributor admin, string role)
    {
        var email = $"invitee-{Guid.NewGuid():N}@example.com";
        var response = await TestAuth.CreateClientWithToken(_factory, admin.AccessToken)
            .PostAsJsonAsync("/api/v1/employees", new { fullName = "Anita Rao", email, roles = new[] { role } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var employeeId = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("employee").GetProperty("id").GetGuid();
        return new Invitee(employeeId, email);
    }

    private Task<HttpResponseMessage> SetPasswordAsync(Guid userId, string token, string newPassword) =>
        _anonymous.PostAsJsonAsync(SetPasswordUrl, new { userId, token, newPassword });

    private static async Task<string?> MessageOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();
}
