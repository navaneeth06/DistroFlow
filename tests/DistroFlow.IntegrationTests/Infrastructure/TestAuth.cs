using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DistroFlow.IntegrationTests.Infrastructure;

public sealed record RegisteredDistributor(
    Guid TenantId,
    Guid EmployeeId,
    Guid UserId,
    string BusinessName,
    string Email,
    string AccessToken);

/// <summary>Helpers for getting authenticated clients in integration tests.</summary>
public static class TestAuth
{
    public const string Password = "Str0ng!Passw0rd";

    /// <summary>Registers a new distributor through the real API and logs its owner in.</summary>
    public static async Task<RegisteredDistributor> RegisterAndLoginAsync(HttpClient client)
    {
        var businessName = $"Distributor {Guid.NewGuid():N}";
        var email = $"owner-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            businessName,
            timeZoneId = "Asia/Kolkata",
            ownerFullName = "Ravi Sharma",
            email,
            password = Password
        });
        register.EnsureSuccessStatusCode();
        var data = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("accessToken").GetString()!;

        return new RegisteredDistributor(
            data.GetProperty("tenantId").GetGuid(),
            data.GetProperty("employeeId").GetGuid(),
            data.GetProperty("userId").GetGuid(),
            businessName,
            email,
            token);
    }

    /// <summary>
    /// Issues a valid token for a tenant with chosen roles, using the app's real signing settings.
    /// Lets tests act as e.g. Sales Staff before employee management exists.
    /// </summary>
    public static string IssueToken(ApiIntegrationTestFactory factory, Guid tenantId, params string[] roles)
    {
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>();

        return new JwtTokenService(options, TimeProvider.System)
            .CreateAccessToken(new TokenSubject(Guid.CreateVersion7(), Guid.CreateVersion7(), tenantId, roles))
            .Token;
    }

    public static HttpClient CreateClientWithToken(ApiIntegrationTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
