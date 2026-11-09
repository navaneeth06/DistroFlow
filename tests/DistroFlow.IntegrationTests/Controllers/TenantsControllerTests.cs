
using System.Net;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.IntegrationTests.Infrastructure;

namespace DistroFlow.IntegrationTests.Controllers;

public class TenantsControllerTests
    : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly HttpClient _client;

    private readonly ApiIntegrationTestFactory _factory;

    public TenantsControllerTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

  
    [Fact]
    public async Task CreateTenant_ViaTenantsEndpoint_IsNotAllowed()
    {
        // Tenants are created only through /api/v1/auth/register, together with their owner.
        var request = new { name = "Should Not Be Created", timeZoneId = "Asia/Kolkata" };

        var response = await _client.PostAsJsonAsync("/api/v1/tenants", request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTenants_ReturnsTenantList()
    {
        // Arrange
        await SeedTenantAsync("GetAll Test Distributors");

        // Act
        var response = await _client.GetAsync("/api/v1/tenants");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("success").GetBoolean());

        var tenants = body.GetProperty("data");

        Assert.Equal(JsonValueKind.Array, tenants.ValueKind);

        Assert.Contains(
            tenants.EnumerateArray(),
            tenant => tenant.GetProperty("name").GetString()
                == "GetAll Test Distributors"
        );
    }

    [Fact]
    public async Task GetTenantById_WithExistingId_ReturnsTenant()
    {
        // Arrange
        var tenantId = await SeedTenantAsync("GetById Test Distributors");

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/tenants/{tenantId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("success").GetBoolean());

        var tenant = body.GetProperty("data");

        Assert.Equal(tenantId, tenant.GetProperty("id").GetGuid());

        Assert.Equal(
            "GetById Test Distributors",
            tenant.GetProperty("name").GetString()
        );
    }

    [Fact]
    public async Task GetTenantById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Guid.CreateVersion7();

        // Act
        var response = await _client.GetAsync(
            $"/api/v1/tenants/{nonExistingId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("success").GetBoolean());

        Assert.Equal(
            "Tenant not found.",
            body.GetProperty("message").GetString()
        );

        Assert.Equal(
            JsonValueKind.Null,
            body.GetProperty("data").ValueKind
        );
    }

    [Fact]
    public async Task UpdateTenant_WithExistingId_UpdatesTenant()
    {
        // Arrange
        var tenantId = await SeedTenantAsync("Original Distributor");

        var updateRequest = new
        {
            name = "Updated Distributor",
            email = "updated@test.com",
            phone = "+919876543210",
            currencyCode = "USD",
            invoicePrefix = "UPD",
            fiscalYearStartMonth = 1,
            timeZoneId = "Asia/Kolkata",
            isActive = true
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/tenants/{tenantId}",
            updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("success").GetBoolean());

        var tenant = body.GetProperty("data");

        Assert.Equal("Updated Distributor", tenant.GetProperty("name").GetString());
        Assert.Equal("updated@test.com", tenant.GetProperty("email").GetString());
        Assert.Equal("USD", tenant.GetProperty("currencyCode").GetString());
        Assert.Equal("UPD", tenant.GetProperty("invoicePrefix").GetString());
        Assert.Equal(1, tenant.GetProperty("fiscalYearStartMonth").GetInt32());
        Assert.True(tenant.GetProperty("isActive").GetBoolean());

        Assert.NotNull(tenant.GetProperty("updatedAtUtc").GetString());
    }

    [Fact]
    public async Task UpdateTenant_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Guid.CreateVersion7();

        var updateRequest = new
        {
            name = "Updated Distributor",
            email = "updated@test.com",
            phone = "+919876543210",
            currencyCode = "USD",
            invoicePrefix = "UPD",
            fiscalYearStartMonth = 1,
            timeZoneId = "Asia/Kolkata",
            isActive = true
        };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/tenants/{nonExistingId}",
            updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("success").GetBoolean());

        Assert.Equal(
            "Tenant not found.",
            body.GetProperty("message").GetString()
        );

        Assert.Equal(
            JsonValueKind.Null,
            body.GetProperty("data").ValueKind
        );
    }
    [Fact]
    public async Task DeactivateTenant_WithExistingId_DeactivatesTenant()
    {
        // Arrange
        var tenantId = await SeedTenantAsync("Deactivate Test Distributor");

        // Act
        var response = await _client.PatchAsync(
            $"/api/v1/tenants/{tenantId}/deactivate",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("success").GetBoolean());

        // Verify database state
        using var scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleAsync(t => t.Id == tenantId);

        Assert.False(tenant.IsActive);
        Assert.NotNull(tenant.UpdatedAtUtc);
    }
    [Fact]
    public async Task DeactivateTenant_WhenAlreadyInactive_ReturnsSuccess()
    {
        // Arrange
        var tenantId = await SeedTenantAsync("Already Inactive Distributor");

        // First deactivation
        var firstResponse = await _client.PatchAsync(
            $"/api/v1/tenants/{tenantId}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act: deactivate again
        var secondResponse = await _client.PatchAsync(
            $"/api/v1/tenants/{tenantId}/deactivate",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var body = await secondResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("success").GetBoolean());
    }
    [Fact]
    public async Task DeactivateTenant_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Guid.CreateVersion7();

        // Act
        var response = await _client.PatchAsync(
            $"/api/v1/tenants/{nonExistingId}/deactivate",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("success").GetBoolean());

        Assert.Equal(
            "Tenant not found.",
            body.GetProperty("message").GetString()
        );

        Assert.Equal(
            JsonValueKind.Null,
            body.GetProperty("data").ValueKind
        );
    }

    private async Task<Guid> SeedTenantAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var tenant = new Tenant { Name = name };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        return tenant.Id;
    }
}
