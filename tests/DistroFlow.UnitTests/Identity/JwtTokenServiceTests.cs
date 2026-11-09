using System.Text;
using DistroFlow.Application.Common;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
using DistroFlow.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DistroFlow.UnitTests.Identity;

public class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Options = new()
    {
        Issuer = "DistroFlow.Api",
        Audience = "DistroFlow.Web",
        SigningKey = "unit-tests-signing-key-0123456789-abcdefghij",
        AccessTokenMinutes = 30
    };

    private static readonly TokenSubject Subject = new(
        UserId: Guid.CreateVersion7(),
        EmployeeId: Guid.CreateVersion7(),
        TenantId: Guid.CreateVersion7(),
        Roles: [EmployeeRoles.Administrator, EmployeeRoles.Approver]);

    private static JwtTokenService CreateService() =>
        new(Microsoft.Extensions.Options.Options.Create(Options), new FixedTimeProvider(Now));

    [Fact]
    public void Token_contains_user_employee_tenant_and_all_roles()
    {
        var token = new JsonWebToken(CreateService().CreateAccessToken(Subject).Token);

        Assert.Equal(Subject.UserId.ToString(), token.GetClaim(AppClaimTypes.UserId).Value);
        Assert.Equal(Subject.EmployeeId.ToString(), token.GetClaim(AppClaimTypes.EmployeeId).Value);
        Assert.Equal(Subject.TenantId.ToString(), token.GetClaim(AppClaimTypes.TenantId).Value);

        var roles = token.Claims.Where(c => c.Type == AppClaimTypes.Role).Select(c => c.Value);
        Assert.Equal(Subject.Roles.Order(), roles.Order());

        Assert.False(string.IsNullOrEmpty(token.Id));
        Assert.Equal(Options.Issuer, token.Issuer);
        Assert.Contains(Options.Audience, token.Audiences);
    }

    [Fact]
    public void Token_expires_after_configured_minutes()
    {
        var accessToken = CreateService().CreateAccessToken(Subject);
        var token = new JsonWebToken(accessToken.Token);

        var expected = Now.UtcDateTime.AddMinutes(30);
        Assert.Equal(expected, accessToken.ExpiresAtUtc);
        Assert.Equal(expected, token.ValidTo);
        Assert.Equal(Now.UtcDateTime, token.IssuedAt);
    }

    [Fact]
    public void Each_token_has_a_unique_id()
    {
        var service = CreateService();

        var first = new JsonWebToken(service.CreateAccessToken(Subject).Token);
        var second = new JsonWebToken(service.CreateAccessToken(Subject).Token);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Token_is_signed_with_the_configured_key()
    {
        var token = CreateService().CreateAccessToken(Subject).Token;
        var handler = new JsonWebTokenHandler();

        var withRightKey = await handler.ValidateTokenAsync(token, ValidationParameters(Options.SigningKey));
        var withWrongKey = await handler.ValidateTokenAsync(token, ValidationParameters("some-other-key-0123456789-abcdefghijklmn"));

        Assert.True(withRightKey.IsValid, withRightKey.Exception?.Message);
        Assert.False(withWrongKey.IsValid);
        Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(withWrongKey.Exception);
    }

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidIssuer = Options.Issuer,
        ValidAudience = Options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        // Issued at a fixed past time; lifetime is covered by Token_expires_after_configured_minutes.
        ValidateLifetime = false
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
