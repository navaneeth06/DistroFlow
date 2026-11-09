using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Domain.Constants;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Controllers;

public class TenantControllerTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string TenantUrl = "/api/v1/tenant";

    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _anonymous;

    public TenantControllerTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task Get_without_token_returns_401()
    {
        var response = await _anonymous.GetAsync(TenantUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_the_callers_own_tenant()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        await TestAuth.RegisterAndLoginAsync(_anonymous); // another distributor exists too

        var response = await TestAuth.CreateClientWithToken(_factory, a.AccessToken).GetAsync(TenantUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(a.TenantId, data.GetProperty("id").GetGuid());
        Assert.Equal(a.BusinessName, data.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Any_employee_role_can_read_their_tenant()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var salesToken = TestAuth.IssueToken(_factory, a.TenantId, EmployeeRoles.SalesStaff);

        var response = await TestAuth.CreateClientWithToken(_factory, salesToken).GetAsync(TenantUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_updates_own_tenant_and_other_tenants_are_untouched()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var b = await TestAuth.RegisterAndLoginAsync(_anonymous);

        var response = await TestAuth.CreateClientWithToken(_factory, a.AccessToken)
            .PutAsJsonAsync(TenantUrl, ValidUpdate("Updated Distributor"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("Updated Distributor", data.GetProperty("name").GetString());
        Assert.Equal("USD", data.GetProperty("currencyCode").GetString());
        Assert.Equal("UPD", data.GetProperty("invoicePrefix").GetString());
        Assert.NotNull(data.GetProperty("updatedAtUtc").GetString());

        Assert.Equal("Updated Distributor", (await LoadTenantAsync(a.TenantId)).Name);
        Assert.Equal(b.BusinessName, (await LoadTenantAsync(b.TenantId)).Name);
    }

    [Fact]
    public async Task Non_administrator_cannot_update_tenant()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var salesToken = TestAuth.IssueToken(_factory, a.TenantId, EmployeeRoles.SalesStaff);

        var response = await TestAuth.CreateClientWithToken(_factory, salesToken)
            .PutAsJsonAsync(TenantUrl, ValidUpdate("Hijacked Name"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(a.BusinessName, (await LoadTenantAsync(a.TenantId)).Name);
    }

    [Fact]
    public async Task Update_cannot_change_active_status()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        await SetTenantActiveAsync(a.TenantId, isActive: true);

        // isActive is not part of the request; sending it must have no effect.
        var body = new
        {
            name = "Still Active Distributor",
            currencyCode = "INR",
            invoicePrefix = "INV",
            fiscalYearStartMonth = 4,
            timeZoneId = "Asia/Kolkata",
            isActive = false
        };
        var response = await TestAuth.CreateClientWithToken(_factory, a.AccessToken).PutAsJsonAsync(TenantUrl, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await LoadTenantAsync(a.TenantId)).IsActive);
    }

    [Fact]
    public async Task Update_with_invalid_body_returns_400()
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var invalid = new
        {
            name = "X", // too short
            currencyCode = "usd", // must be 3 uppercase letters
            invoicePrefix = "INV",
            fiscalYearStartMonth = 13, // out of range
            timeZoneId = "Not/AZone"
        };

        var response = await TestAuth.CreateClientWithToken(_factory, a.AccessToken).PutAsJsonAsync(TenantUrl, invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("CurrencyCode", out _));
        Assert.True(errors.TryGetProperty("FiscalYearStartMonth", out _));
        Assert.True(errors.TryGetProperty("TimeZoneId", out _));
    }

    [Theory]
    [InlineData("GET", "/api/v1/tenants")]
    [InlineData("GET", "/api/v1/tenants/0195f3a0-0000-7000-8000-000000000001")]
    [InlineData("PATCH", "/api/v1/tenants/0195f3a0-0000-7000-8000-000000000001/deactivate")]
    public async Task Old_cross_tenant_endpoints_no_longer_exist(string method, string url)
    {
        var a = await TestAuth.RegisterAndLoginAsync(_anonymous);
        var client = TestAuth.CreateClientWithToken(_factory, a.AccessToken);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object ValidUpdate(string name) => new
    {
        name,
        email = "updated@test.com",
        phone = "+919876543210",
        currencyCode = "USD",
        invoicePrefix = "UPD",
        fiscalYearStartMonth = 1,
        timeZoneId = "Asia/Kolkata"
    };

    private async Task<Domain.Entities.Tenant> LoadTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
    }

    private async Task SetTenantActiveAsync(Guid tenantId, bool isActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId);
        tenant.IsActive = isActive;
        await db.SaveChangesAsync();
    }
}
