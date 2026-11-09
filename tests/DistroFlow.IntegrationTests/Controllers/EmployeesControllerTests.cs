using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Controllers;

public class EmployeesControllerTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string EmployeesUrl = "/api/v1/employees";

    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _anonymous;

    public EmployeesControllerTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task List_returns_only_own_employees_with_email_and_roles()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var anita = await AddEmployeeAsync(a.TenantId, "Anita Rao", EmployeeRoles.SalesStaff);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);

        var response = await AsAdmin(a).GetAsync(EmployeesUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .EnumerateArray().ToList();

        // Ordered by name: "Anita Rao" before "Ravi Sharma" (the owner).
        Assert.Equal([anita.EmployeeId, a.EmployeeId], list.Select(e => e.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(list, e => e.GetProperty("id").GetGuid() == b.EmployeeId);

        var owner = list[1];
        Assert.Equal(a.Email, owner.GetProperty("email").GetString());
        Assert.Equal([EmployeeRoles.Administrator], RolesOf(owner));
        Assert.Equal("Active", owner.GetProperty("status").GetString());

        Assert.Equal(anita.Email, list[0].GetProperty("email").GetString());
        Assert.Equal([EmployeeRoles.SalesStaff], RolesOf(list[0]));
    }

    [Fact]
    public async Task Get_by_id_returns_employee_details()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var anita = await AddEmployeeAsync(a.TenantId, "Anita Rao", EmployeeRoles.DispatchStaff);

        var response = await AsAdmin(a).GetAsync($"{EmployeesUrl}/{anita.EmployeeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Anita Rao", data.GetProperty("fullName").GetString());
        Assert.Equal(anita.Email, data.GetProperty("email").GetString());
        Assert.Equal([EmployeeRoles.DispatchStaff], RolesOf(data));
    }

    [Fact]
    public async Task Get_another_distributors_employee_returns_404_not_403()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);

        var response = await AsAdmin(a).GetAsync($"{EmployeesUrl}/{b.EmployeeId}");

        // 404, not 403: A must not even learn that B's employee id exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_administrator_cannot_list_employees()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var salesToken = TestAuth.IssueToken(_factory, a.TenantId, EmployeeRoles.SalesStaff);

        var response = await TestAuth.CreateClientWithToken(_factory, salesToken).GetAsync(EmployeesUrl);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_without_token_returns_401()
    {
        var response = await _anonymous.GetAsync(EmployeesUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Invite_creates_employee_without_password_and_emails_a_valid_set_password_link()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var email = UniqueEmail();

        var response = await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(email, "Anita Rao",
            [EmployeeRoles.SalesStaff, EmployeeRoles.Approver], employeeCode: "EMP-7"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(data.GetProperty("inviteEmailSent").GetBoolean());
        var created = data.GetProperty("employee");
        var employeeId = created.GetProperty("id").GetGuid();
        Assert.Equal("Anita Rao", created.GetProperty("fullName").GetString());
        Assert.Equal(email, created.GetProperty("email").GetString());
        Assert.Equal("EMP-7", created.GetProperty("employeeCode").GetString());
        Assert.Equal([EmployeeRoles.Approver, EmployeeRoles.SalesStaff], RolesOf(created));

        // Belongs to the inviting distributor.
        var listed = await AsAdmin(a).GetAsync($"{EmployeesUrl}/{employeeId}");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        // The email links to the frontend's set-password page with a token that Identity accepts.
        var mail = _factory.Emails.SingleTo(email);
        Assert.Contains("Anita Rao", mail.HtmlBody);
        Assert.Contains(a.BusinessName, mail.Subject);
        var (userId, token) = FakeEmailSender.ParseSetPasswordLink(mail.TextBody);

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.False(await userManager.HasPasswordAsync(user));
        Assert.True(await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose,
            token));
    }

    [Fact]
    public async Task Invited_employee_cannot_log_in_before_setting_a_password()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var email = UniqueEmail();
        await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(email, "Anita Rao", [EmployeeRoles.SalesStaff]));

        var login = await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "" });
        var loginWithGuess = await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Str0ng!Passw0rd" });

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode); // empty password fails validation
        Assert.Equal(HttpStatusCode.Unauthorized, loginWithGuess.StatusCode);
    }

    [Fact]
    public async Task Invite_with_email_active_at_another_distributor_returns_409_and_saves_nothing()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);

        // B's owner email is active, so A cannot invite it.
        var response = await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(b.Email, "Copied Person", [EmployeeRoles.SalesStaff]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await ListIdsAsync(a)); // still only A's owner
    }

    [Fact]
    public async Task Duplicate_employee_code_returns_409_and_rolls_back_the_login()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var first = await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(UniqueEmail(), "First", [EmployeeRoles.SalesStaff], employeeCode: "EMP-1"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var secondEmail = UniqueEmail();
        var second = await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(secondEmail, "Second", [EmployeeRoles.SalesStaff], employeeCode: "EMP-1"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(2, (await ListIdsAsync(a)).Count); // owner + first

        // The login created for the second invite was rolled back, so the email is free again.
        var retry = await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(secondEmail, "Second", [EmployeeRoles.SalesStaff], employeeCode: "EMP-2"));
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Theory]
    [InlineData("")]                         // no roles
    [InlineData("Super Admin")]              // unknown role
    [InlineData("Sales Staff,Sales Staff")]  // duplicate
    public async Task Invite_with_invalid_roles_returns_400(string commaSeparatedRoles)
    {
        var roles = commaSeparatedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);

        var response = await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(UniqueEmail(), "Anita Rao", roles));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Roles", out _));
    }

    [Fact]
    public async Task Non_administrator_cannot_invite()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var salesToken = TestAuth.IssueToken(_factory, a.TenantId, EmployeeRoles.SalesStaff);

        var response = await TestAuth.CreateClientWithToken(_factory, salesToken)
            .PostAsJsonAsync(EmployeesUrl, InviteBody(UniqueEmail(), "Anita Rao", [EmployeeRoles.SalesStaff]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task When_email_cannot_be_sent_employee_is_still_created_and_response_says_so()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var email = $"{FakeEmailSender.FailingRecipientMarker}-{Guid.NewGuid():N}@example.com";

        var response = await AsAdmin(a).PostAsJsonAsync(EmployeesUrl, InviteBody(email, "Anita Rao", [EmployeeRoles.SalesStaff]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(data.GetProperty("inviteEmailSent").GetBoolean());
        Assert.Contains(data.GetProperty("employee").GetProperty("id").GetGuid(), await ListIdsAsync(a));
    }

    [Fact]
    public async Task Resend_invite_emails_a_new_link_to_a_pending_employee()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var (employeeId, email) = await InviteAsync(a);

        var response = await AsAdmin(a).PostAsync($"{EmployeesUrl}/{employeeId}/resend-invite", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(data.GetProperty("inviteEmailSent").GetBoolean());
        Assert.Equal(2, _factory.Emails.Sent.Count(m => m.To == email)); // original + resent
    }

    [Fact]
    public async Task Resend_invite_for_employee_who_already_set_a_password_returns_409()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);

        // The owner registered with a password, so there is no pending invite.
        var response = await AsAdmin(a).PostAsync($"{EmployeesUrl}/{a.EmployeeId}/resend-invite", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Resend_invite_for_deactivated_employee_returns_409_and_sends_nothing()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var (employeeId, email) = await InviteAsync(a);
        await DeactivateDirectlyAsync(employeeId);

        var response = await AsAdmin(a).PostAsync($"{EmployeesUrl}/{employeeId}/resend-invite", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, _factory.Emails.Sent.Count(m => m.To == email)); // only the original invite
    }

    [Fact]
    public async Task Resend_invite_for_another_distributors_employee_returns_404()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var (employeeOfB, email) = await InviteAsync(b);

        var response = await AsAdmin(a).PostAsync($"{EmployeesUrl}/{employeeOfB}/resend-invite", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, _factory.Emails.Sent.Count(m => m.To == email));
    }

    private async Task<(Guid EmployeeId, string Email)> InviteAsync(RegisteredDistributor admin)
    {
        var email = UniqueEmail();
        var response = await AsAdmin(admin).PostAsJsonAsync(EmployeesUrl, InviteBody(email, "Anita Rao", [EmployeeRoles.SalesStaff]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return (data.GetProperty("employee").GetProperty("id").GetGuid(), email);
    }

    /// <summary>Test setup until the deactivate endpoint exists.</summary>
    private async Task DeactivateDirectlyAsync(Guid employeeId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var employee = await db.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == employeeId);
        var user = await db.Users.SingleAsync(u => u.Id == employee.UserId);
        employee.Deactivate(DateTime.UtcNow);
        user.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static string UniqueEmail() => $"invitee-{Guid.NewGuid():N}@example.com";

    private static object InviteBody(string email, string fullName, string[] roles, string? employeeCode = null) =>
        new { fullName, email, phone = "+919876543210", employeeCode, roles };

    private async Task<List<Guid>> ListIdsAsync(RegisteredDistributor distributor)
    {
        var list = await AsAdmin(distributor).GetFromJsonAsync<JsonElement>(EmployeesUrl);
        return list.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
    }

    private HttpClient AsAdmin(RegisteredDistributor distributor) =>
        TestAuth.CreateClientWithToken(_factory, distributor.AccessToken);

    private static string[] RolesOf(JsonElement employee) =>
        employee.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).ToArray();

    /// <summary>
    /// Test setup until the invite endpoint exists: creates a login and an employee directly.
    /// </summary>
    private async Task<(Guid EmployeeId, string Email)> AddEmployeeAsync(Guid tenantId, string fullName, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = $"staff-{Guid.NewGuid():N}@example.com";
        var login = await identity.CreateUserAsync(tenantId, email, TestAuth.Password, role);
        Assert.True(login.Succeeded);

        var employee = new Employee { TenantId = tenantId, UserId = login.UserId, FullName = fullName };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        return (employee.Id, email);
    }
}
