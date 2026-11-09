namespace DistroFlow.Application.Interfaces.Identity;

public interface ITokenService
{
    AccessToken CreateAccessToken(TokenSubject subject);
}

/// <summary>Who the token is for. Everything here ends up as claims in the token.</summary>
public sealed record TokenSubject(
    Guid UserId,
    Guid EmployeeId,
    Guid TenantId,
    IReadOnlyCollection<string> Roles);

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
