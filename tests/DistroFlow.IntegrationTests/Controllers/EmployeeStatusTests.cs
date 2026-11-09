using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Controllers;

/// <summary>Deactivating (and later reactivating) employees.</summary>
public class EmployeeStatusTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string EmployeePassword = "Empl0yee!Passw0rd";

    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _anonymous;

    public EmployeeStatusTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task Deactivated_employee_can_no_longer_log_in()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employee = await InviteAndAcceptAsync(owner, EmployeeRoles.SalesStaff);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(employee.Email)).StatusCode);

        var response = await PostAsync(owner, $"{employee.Id}/deactivate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Deactivated", data.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(employee.Email)).StatusCode);
    }

    [Fact]
    public async Task Deactivating_twice_is_safe()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employee = await InviteAndAcceptAsync(owner, EmployeeRoles.SalesStaff);

        await PostAsync(owner, $"{employee.Id}/deactivate");
        var again = await PostAsync(owner, $"{employee.Id}/deactivate");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Deactivated_employees_email_can_be_used_by_another_distributor()
    {
        // Your rule: once someone leaves (deactivated), they can join another distributor.
        var oldEmployer = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employee = await InviteAndAcceptAsync(oldEmployer, EmployeeRoles.SalesStaff);
        var newEmployer = await TestAuth.RegisterAndLoginAsync(_anonymous);

        var beforeLeaving = await InviteAsync(newEmployer, employee.Email, EmployeeRoles.SalesStaff);
        await PostAsync(oldEmployer, $"{employee.Id}/deactivate");
        var afterLeaving = await InviteAsync(newEmployer, employee.Email, EmployeeRoles.SalesStaff);

        Assert.Equal(HttpStatusCode.Conflict, beforeLeaving.StatusCode);
        Assert.Equal(HttpStatusCode.Created, afterLeaving.StatusCode);
    }

    [Fact]
    public async Task Last_active_administrator_cannot_be_deactivated()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);

        var response = await PostAsync(owner, $"{owner.EmployeeId}/deactivate");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(owner.Email, TestAuth.Password)).StatusCode);
    }

    [Fact]
    public async Task Deactivating_another_distributors_employee_returns_404()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employeeOfB = await InviteAndAcceptAsync(b, EmployeeRoles.SalesStaff);

        var response = await PostAsync(a, $"{employeeOfB.Id}/deactivate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(employeeOfB.Email)).StatusCode);
    }

    [Fact]
    public async Task Two_administrators_deactivating_each_other_at_the_same_time_leave_exactly_one()
    {
        const int distributors = 5;
        var setups = await Task.WhenAll(Enumerable.Range(0, distributors).Select(async _ =>
        {
            var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
            var secondAdmin = await InviteAndAcceptAsync(owner, EmployeeRoles.Administrator);
            return (owner, secondAdmin);
        }));

        var results = await Task.WhenAll(setups.Select(async s =>
        {
            var responses = await Task.WhenAll(
                PostAsync(s.owner, $"{s.owner.EmployeeId}/deactivate"),
                PostAsync(s.owner, $"{s.secondAdmin.Id}/deactivate"));
            return (s.owner.TenantId, Statuses: responses.Select(r => r.StatusCode).ToArray());
        }));

        foreach (var (tenantId, statuses) in results)
        {
            Assert.Single(statuses, HttpStatusCode.OK);
            Assert.Single(statuses, HttpStatusCode.Conflict);
            Assert.Equal(1, await CountActiveAdministratorsAsync(tenantId));
        }
    }

    [Fact]
    public async Task Reactivated_employee_can_log_in_again_with_their_old_password()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employee = await InviteAndAcceptAsync(owner, EmployeeRoles.SalesStaff);
        await PostAsync(owner, $"{employee.Id}/deactivate");

        var response = await PostAsync(owner, $"{employee.Id}/reactivate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Active", data.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(employee.Email)).StatusCode);
    }

    [Fact]
    public async Task Reactivating_an_active_employee_is_safe()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employee = await InviteAndAcceptAsync(owner, EmployeeRoles.SalesStaff);

        var response = await PostAsync(owner, $"{employee.Id}/reactivate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reactivating_fails_when_the_email_is_now_active_at_another_distributor()
    {
        // The person left the old job, joined another distributor, then the old one tries to bring them back.
        var oldEmployer = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employee = await InviteAndAcceptAsync(oldEmployer, EmployeeRoles.SalesStaff);
        await PostAsync(oldEmployer, $"{employee.Id}/deactivate");
        var newEmployer = await TestAuth.RegisterAndLoginAsync(_anonymous);
        Assert.Equal(HttpStatusCode.Created, (await InviteAsync(newEmployer, employee.Email, EmployeeRoles.SalesStaff)).StatusCode);

        var response = await PostAsync(oldEmployer, $"{employee.Id}/reactivate");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Nothing changed at the old employer: still deactivated (the employee change was rolled back).
        var atOldEmployer = (await TestAuth.CreateClientWithToken(_factory, oldEmployer.AccessToken)
            .GetFromJsonAsync<JsonElement>($"/api/v1/employees/{employee.Id}")).GetProperty("data");
        Assert.Equal("Deactivated", atOldEmployer.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reactivating_another_distributors_employee_returns_404()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employeeOfB = await InviteAndAcceptAsync(b, EmployeeRoles.SalesStaff);
        await PostAsync(b, $"{employeeOfB.Id}/deactivate");

        var response = await PostAsync(a, $"{employeeOfB.Id}/reactivate");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(employeeOfB.Email)).StatusCode);
    }

    private sealed record AcceptedEmployee(Guid Id, string Email);

    /// <summary>Invites an employee and sets their password via the emailed link, like a real person would.</summary>
    private async Task<AcceptedEmployee> InviteAndAcceptAsync(RegisteredDistributor admin, string role)
    {
        var email = $"status-{Guid.NewGuid():N}@example.com";
        var invite = await InviteAsync(admin, email, role);
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var employeeId = (await invite.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("employee").GetProperty("id").GetGuid();

        var (userId, token) = _factory.Emails.SetPasswordLinkTo(email);
        var set = await _anonymous.PostAsJsonAsync("/api/v1/auth/set-password",
            new { userId, token, newPassword = EmployeePassword });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        return new AcceptedEmployee(employeeId, email);
    }

    private Task<HttpResponseMessage> InviteAsync(RegisteredDistributor admin, string email, string role) =>
        TestAuth.CreateClientWithToken(_factory, admin.AccessToken)
            .PostAsJsonAsync("/api/v1/employees", new { fullName = "Anita Rao", email, roles = new[] { role } });

    private Task<HttpResponseMessage> PostAsync(RegisteredDistributor admin, string path) =>
        TestAuth.CreateClientWithToken(_factory, admin.AccessToken).PostAsync($"/api/v1/employees/{path}", null);

    private Task<HttpResponseMessage> LoginAsync(string email, string password = EmployeePassword) =>
        _anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    private async Task<int> CountActiveAdministratorsAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>().CountActiveAdministratorsAsync(tenantId);
    }
}
