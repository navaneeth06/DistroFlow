namespace DistroFlow.Application.Interfaces.Identity;

public enum CredentialCheckError
{
    None = 0,

    /// <summary>No active account with this email, or wrong password.</summary>
    InvalidCredentials = 1,

    /// <summary>Too many failed attempts; the account is temporarily locked.</summary>
    LockedOut = 2
}

public sealed record CredentialCheckResult
{
    public Guid UserId { get; private init; }

    public Guid TenantId { get; private init; }

    public IReadOnlyList<string> Roles { get; private init; } = [];

    public CredentialCheckError Error { get; private init; }

    public bool Succeeded => Error == CredentialCheckError.None;

    public static CredentialCheckResult Success(Guid userId, Guid tenantId, IReadOnlyList<string> roles) =>
        new() { UserId = userId, TenantId = tenantId, Roles = roles };

    public static CredentialCheckResult Failure(CredentialCheckError error) => new() { Error = error };
}
