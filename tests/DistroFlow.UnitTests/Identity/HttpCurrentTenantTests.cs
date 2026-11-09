using System.Security.Claims;
using DistroFlow.Api.Services;
using DistroFlow.Application.Common;
using Microsoft.AspNetCore.Http;

namespace DistroFlow.UnitTests.Identity;

public class HttpCurrentTenantTests
{
    [Fact]
    public void Returns_tenant_id_from_token_claim()
    {
        var tenantId = Guid.CreateVersion7();
        var currentTenant = CreateWithClaims(new Claim(AppClaimTypes.TenantId, tenantId.ToString()));

        Assert.Equal(tenantId, currentTenant.TenantId);
    }

    [Fact]
    public void Returns_null_when_there_is_no_request()
    {
        // e.g. code running outside an HTTP request (background job, startup).
        var currentTenant = new HttpCurrentTenant(new HttpContextAccessor { HttpContext = null });

        Assert.Null(currentTenant.TenantId);
    }

    [Fact]
    public void Returns_null_for_anonymous_request()
    {
        // e.g. POST /auth/login or /auth/register: no token, no claims.
        var currentTenant = CreateWithClaims();

        Assert.Null(currentTenant.TenantId);
    }

    [Fact]
    public void Returns_null_when_claim_is_not_a_guid()
    {
        var currentTenant = CreateWithClaims(new Claim(AppClaimTypes.TenantId, "not-a-guid"));

        Assert.Null(currentTenant.TenantId);
    }

    private static HttpCurrentTenant CreateWithClaims(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims))
        };

        return new HttpCurrentTenant(new HttpContextAccessor { HttpContext = httpContext });
    }
}
