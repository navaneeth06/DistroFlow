using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DistroFlow.IntegrationTests.Controllers;

/// <summary>
/// End-to-end proof that bearer authentication accepts only tokens we issued, unmodified and unexpired.
/// </summary>
public class MeControllerTests : IClassFixture<ApiIntegrationTestFactory>
{
    private const string MeUrl = "/api/v1/me";
    private const string Password = "Str0ng!Passw0rd";

    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public MeControllerTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Without_token_returns_401()
    {
        var response = await _client.GetAsync(MeUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task With_valid_token_returns_identity_from_token()
    {
        var (token, tenantId) = await RegisterAndLoginAsync();

        var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(tenantId, data.GetProperty("tenantId").GetGuid());
        Assert.NotEqual(Guid.Empty, data.GetProperty("userId").GetGuid());
        Assert.NotEqual(Guid.Empty, data.GetProperty("employeeId").GetGuid());
        Assert.Equal([EmployeeRoles.Administrator],
            data.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Token_with_tampered_tenant_id_returns_401()
    {
        var (token, _) = await RegisterAndLoginAsync();

        // Swap tenant_id in the payload but keep the original signature.
        var parts = token.Split('.');
        var payload = Base64UrlEncoder.Decode(parts[1]);
        var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload)!;
        var forged = json.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
        forged["tenant_id"] = Guid.CreateVersion7().ToString();
        parts[1] = Base64UrlEncoder.Encode(JsonSerializer.Serialize(forged));

        var response = await GetMeAsync(string.Join('.', parts));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_another_key_returns_401()
    {
        var options = JwtOptionsFromApp();
        var otherKeyOptions = new JwtOptions
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            SigningKey = "attacker-chosen-signing-key-0123456789-abcdefgh",
            AccessTokenMinutes = options.AccessTokenMinutes
        };

        var token = IssueToken(otherKeyOptions, TimeProvider.System);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Expired_token_returns_401()
    {
        // Issued two hours ago with the real key: signature is valid, lifetime is not.
        var twoHoursAgo = new FixedTimeProvider(DateTimeOffset.UtcNow.AddHours(-2));
        var token = IssueToken(JwtOptionsFromApp(), twoHoursAgo);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Token_for_another_audience_returns_401()
    {
        var options = JwtOptionsFromApp();
        var otherAudience = new JwtOptions
        {
            Issuer = options.Issuer,
            Audience = "SomeOtherApp",
            SigningKey = options.SigningKey,
            AccessTokenMinutes = options.AccessTokenMinutes
        };

        var token = IssueToken(otherAudience, TimeProvider.System);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetMeAsync(token)).StatusCode);
    }

    private JwtOptions JwtOptionsFromApp() =>
        _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

    private static string IssueToken(JwtOptions options, TimeProvider time) =>
        new JwtTokenService(Options.Create(options), time)
            .CreateAccessToken(new TokenSubject(
                Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
                [EmployeeRoles.Administrator]))
            .Token;

    private async Task<(string Token, Guid TenantId)> RegisterAndLoginAsync()
    {
        var email = $"me-{Guid.NewGuid():N}@example.com";
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            businessName = $"Distributor {Guid.NewGuid():N}",
            timeZoneId = "Asia/Kolkata",
            ownerFullName = "Ravi Sharma",
            email,
            password = Password
        });
        var tenantId = (await register.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("tenantId").GetGuid();

        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").GetProperty("accessToken").GetString()!;

        return (token, tenantId);
    }

    private Task<HttpResponseMessage> GetMeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
