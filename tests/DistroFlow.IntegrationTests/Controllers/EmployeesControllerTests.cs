using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
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
