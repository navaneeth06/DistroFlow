using System.Security.Claims;
using System.Text;
using DistroFlow.Application.Common;
using DistroFlow.Application.Interfaces.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DistroFlow.Infrastructure.Identity;

public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(TokenSubject subject)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, subject.UserId.ToString()),
            new(AppClaimTypes.EmployeeId, subject.EmployeeId.ToString()),
            new(AppClaimTypes.TenantId, subject.TenantId.ToString()),
            // Unique token id: lets a specific token be identified/revoked later if needed.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(subject.Roles.Select(role => new Claim(AppClaimTypes.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
