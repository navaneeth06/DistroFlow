using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DistroFlow.IntegrationTests.Persistence;

/// <summary>
/// Proves the global tenant filter: a distributor can never read another distributor's employees.
/// </summary>
public class TenantIsolationTests : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public TenantIsolationTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Tenant_sees_only_its_own_employees()
    {
        var a = await RegisterDistributorAsync();
        var b = await RegisterDistributorAsync();

        await using var dbAsA = CreateDbContext(a.TenantId);
        var visible = await dbAsA.Employees.ToListAsync();

        Assert.Contains(visible, e => e.Id == a.EmployeeId);
        Assert.DoesNotContain(visible, e => e.Id == b.EmployeeId);
        Assert.All(visible, e => Assert.Equal(a.TenantId, e.TenantId));
    }

    [Fact]
    public async Task Querying_another_tenants_employee_by_id_returns_nothing()
    {
        var a = await RegisterDistributorAsync();
        var b = await RegisterDistributorAsync();

        await using var dbAsA = CreateDbContext(a.TenantId);
        var employeeOfB = await dbAsA.Employees.SingleOrDefaultAsync(e => e.Id == b.EmployeeId);

        // Even knowing the exact id, A cannot load B's employee.
        Assert.Null(employeeOfB);
    }

    [Fact]
    public async Task Without_a_tenant_no_employees_are_visible()
    {
        var a = await RegisterDistributorAsync();

        await using var anonymous = CreateDbContext(tenantId: null);

        // Fail closed: an unknown tenant sees nothing, never everything.
        Assert.False(await anonymous.Employees.AnyAsync(e => e.Id == a.EmployeeId));
    }

    [Fact]
    public void Every_entity_with_TenantId_has_a_query_filter()
    {
        // Safety net: when a new tenant-owned entity (Order, Product...) is added,
        // this fails until it gets a tenant filter.
        var documentedExceptions = new[] { typeof(ApplicationUser) }; // see CLAUDE.md, Tenancy

        using var db = CreateDbContext(tenantId: null);

        var tenantOwned = db.Model.GetEntityTypes()
            .Where(t => t.FindProperty("TenantId") is not null)
            .Where(t => !documentedExceptions.Contains(t.ClrType))
            .ToList();

        Assert.NotEmpty(tenantOwned);
        Assert.All(tenantOwned, entityType =>
            Assert.True(
                entityType.GetDeclaredQueryFilters().Count > 0,
                $"{entityType.ClrType.Name} has a TenantId but no tenant query filter."));
    }

    private AppDbContext CreateDbContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_factory.ConnectionString)
            .Options;

        return new AppDbContext(options, new FixedCurrentTenant(tenantId));
    }

    private async Task<(Guid TenantId, Guid EmployeeId)> RegisterDistributorAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            businessName = $"Distributor {Guid.NewGuid():N}",
            timeZoneId = "Asia/Kolkata",
            ownerFullName = "Ravi Sharma",
            email = $"iso-{Guid.NewGuid():N}@example.com",
            password = "Str0ng!Passw0rd"
        });
        response.EnsureSuccessStatusCode();

        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        return (data.GetProperty("tenantId").GetGuid(), data.GetProperty("employeeId").GetGuid());
    }

    /// <summary>Stands in for "the logged-in user's tenant" without needing a real request.</summary>
    private sealed class FixedCurrentTenant(Guid? tenantId) : ICurrentTenant
    {
        public Guid? TenantId { get; } = tenantId;
    }
}
