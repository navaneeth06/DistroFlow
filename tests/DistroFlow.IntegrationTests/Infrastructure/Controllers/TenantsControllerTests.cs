
using System.Net;
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
    public async Task CreateTenant_WithValidRequest_PersistsTenantInDatabase()
    {
        // Arrange
        var request = new
        {
            name = "Persistence Test Distributors",
            email = "persistence@test.com",
            currencyCode = "INR",
            invoicePrefix = "PST",
            fiscalYearStartMonth = 4,
            timeZoneId = "Asia/Kolkata"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var tenantId = body
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

        // Verify persistence using EF Core
        using var scope = _factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var savedTenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId);

        Assert.NotNull(savedTenant);

        Assert.Equal(
            "Persistence Test Distributors",
            savedTenant.Name
        );

        Assert.Equal("INR", savedTenant.CurrencyCode);
    }

   
    [Fact]
    public async Task GetAllTenants_ReturnsTenantList()
    {
        // Arrange
        var request = new
        {
            name = "GetAll Test Distributors",
            email = "getall@test.com",
            currencyCode = "INR",
            invoicePrefix = "GET",
            fiscalYearStartMonth = 4,
            timeZoneId = "Asia/Kolkata"
        };

        await _client.PostAsJsonAsync("/api/v1/tenants", request);

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
        var request = new
        {
            name = "GetById Test Distributors",
            email = "getbyid@test.com",
            currencyCode = "INR",
            invoicePrefix = "GID",
            fiscalYearStartMonth = 4,
            timeZoneId = "Asia/Kolkata"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            request);

        var createdBody = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var tenantId = createdBody
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

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
        var createRequest = new
        {
            name = "Original Distributor",
            email = "original@test.com",
            currencyCode = "INR",
            invoicePrefix = "ORG",
            fiscalYearStartMonth = 4,
            timeZoneId = "Asia/Kolkata"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            createRequest);

        var createdBody = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var tenantId = createdBody
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

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
        var createRequest = new
        {
            name = "Deactivate Test Distributor",
            email = "deactivate@test.com",
            currencyCode = "INR",
            invoicePrefix = "DEC",
            fiscalYearStartMonth = 4,
            timeZoneId = "Asia/Kolkata"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            createRequest);

        var createdBody = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var tenantId = createdBody
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

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
        var createRequest = new
        {
            name = "Already Inactive Distributor",
            email = "inactive@test.com",
            currencyCode = "INR",
            invoicePrefix = "INA",
            fiscalYearStartMonth = 4,
            timeZoneId = "Asia/Kolkata"
        };

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            createRequest);

        var createdBody = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var tenantId = createdBody
            .GetProperty("data")
            .GetProperty("id")
            .GetGuid();

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
}
