using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Controllers;

/// <summary>PUT /api/v1/employees/{id}/roles, including the "last administrator" rule under concurrency.</summary>
public class EmployeeRolesTests : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _anonymous;

    public EmployeeRolesTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task Change_roles_replaces_them()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var employeeId = await InviteAsync(owner, EmployeeRoles.SalesStaff);

        var response = await ChangeRolesAsync(owner, employeeId, EmployeeRoles.DispatchStaff, EmployeeRoles.Approver);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).ToArray();
        Assert.Equal([EmployeeRoles.Approver, EmployeeRoles.DispatchStaff], roles);
    }

    [Fact]
    public async Task Last_active_administrator_cannot_be_demoted()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);

        // The owner is the only Administrator and tries to demote themselves.
        var response = await ChangeRolesAsync(owner, owner.EmployeeId, EmployeeRoles.SalesStaff);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountActiveAdministratorsAsync(owner.TenantId));
    }

    [Fact]
    public async Task An_administrator_can_be_demoted_when_another_one_exists()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
        await InviteAsync(owner, EmployeeRoles.Administrator);

        var response = await ChangeRolesAsync(owner, owner.EmployeeId, EmployeeRoles.SalesStaff);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await CountActiveAdministratorsAsync(owner.TenantId));
    }

    [Fact]
    public async Task Changing_roles_of_another_distributors_employee_returns_404()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);
        await InviteAsync(b, EmployeeRoles.Administrator); // so the rule would not block it anyway

        var response = await ChangeRolesAsync(a, b.EmployeeId, EmployeeRoles.SalesStaff);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(2, await CountActiveAdministratorsAsync(b.TenantId));
    }

    [Fact]
    public async Task Invalid_roles_return_400()
    {
        var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);

        var response = await ChangeRolesAsync(owner, owner.EmployeeId, "Super Admin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Two_administrators_demoting_each_other_at_the_same_time_leave_exactly_one()
    {
        // Several distributors at once, each with two admins, to give a race every chance to happen.
        const int distributors = 5;
        var setups = await Task.WhenAll(Enumerable.Range(0, distributors).Select(async _ =>
        {
            var owner = await TestAuth.RegisterAndLoginAsync(_anonymous);
            var secondAdmin = await InviteAsync(owner, EmployeeRoles.Administrator);
            return (owner, secondAdmin);
        }));

        // For every distributor, fire both demotions simultaneously.
        var results = await Task.WhenAll(setups.Select(async s =>
        {
            var responses = await Task.WhenAll(
                ChangeRolesAsync(s.owner, s.owner.EmployeeId, EmployeeRoles.SalesStaff),
                ChangeRolesAsync(s.owner, s.secondAdmin, EmployeeRoles.SalesStaff));
            return (s.owner.TenantId, Statuses: responses.Select(r => r.StatusCode).ToArray());
        }));

        foreach (var (tenantId, statuses) in results)
        {
            // Exactly one demotion wins; the other sees one admin left and is refused.
            Assert.Single(statuses, HttpStatusCode.OK);
            Assert.Single(statuses, HttpStatusCode.Conflict);
            Assert.Equal(1, await CountActiveAdministratorsAsync(tenantId));
        }
    }

    private Task<HttpResponseMessage> ChangeRolesAsync(RegisteredDistributor admin, Guid employeeId, params string[] roles) =>
        TestAuth.CreateClientWithToken(_factory, admin.AccessToken)
            .PutAsJsonAsync($"/api/v1/employees/{employeeId}/roles", new { roles });

    private async Task<Guid> InviteAsync(RegisteredDistributor admin, string role)
    {
        var response = await TestAuth.CreateClientWithToken(_factory, admin.AccessToken)
            .PostAsJsonAsync("/api/v1/employees", new
            {
                fullName = "Anita Rao",
                email = $"roles-{Guid.NewGuid():N}@example.com",
                roles = new[] { role }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("employee").GetProperty("id").GetGuid();
    }

    private async Task<int> CountActiveAdministratorsAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIdentityService>().CountActiveAdministratorsAsync(tenantId);
    }
}
