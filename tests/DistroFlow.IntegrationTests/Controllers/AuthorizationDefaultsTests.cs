using System.Net;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Controllers;

/// <summary>
/// Proves the API is secure by default: login is required everywhere unless an
/// endpoint is deliberately, visibly public.
/// </summary>
public class AuthorizationDefaultsTests : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly ApiIntegrationTestFactory _factory;
    private readonly HttpClient _anonymous;

    public AuthorizationDefaultsTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
    }

    [Fact]
    public async Task Fallback_policy_requires_an_authenticated_user()
    {
        var policyProvider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await policyProvider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void Only_the_expected_endpoints_are_public()
    {
        // If this fails, a new endpoint allows anonymous access. Make sure that is
        // intended, then add it here so the decision is reviewed in the diff.
        string[] expectedPublic =
        [
            "GET health/database",
            "GET openapi/{documentName}.json",
            "POST api/v1/auth/login",
            "POST api/v1/auth/register",
            "POST api/v1/auth/set-password" // invited employee is not logged in yet; protected by the signed token
        ];

        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        var actualPublic = endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["ANY"])
                .Select(method => $"{method} {e.RoutePattern.RawText!.Trim('/')}"))
            .Order()
            .ToArray();

        Assert.Equal(expectedPublic, actualPublic);
    }

    [Fact]
    public async Task Health_check_is_reachable_without_a_token()
    {
        var response = await _anonymous.GetAsync("/health/database");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/tenant")]
    public async Task Protected_endpoints_return_401_without_a_token(string url)
    {
        var response = await _anonymous.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
