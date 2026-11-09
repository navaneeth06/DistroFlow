namespace DistroFlow.Application.Interfaces.Identity;

/// <summary>
/// Creates and manages employee login accounts. Hides ASP.NET Core Identity from the
/// Application layer, which must not depend on Identity types such as ApplicationUser.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates a login account in <paramref name="tenantId"/> with a hashed password and the given role.
    /// Saves immediately; wrap in a transaction when other rows must be saved with it.
    /// </summary>
    Task<CreateUserResult> CreateUserAsync(
        Guid tenantId,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks email + password against the single active account with that email.
    /// Counts failed attempts and enforces lockout.
    /// </summary>
    Task<CredentialCheckResult> CheckCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Email and roles for many logins in a fixed number of queries (not one per employee).
    /// Callers must pass ids of employees already loaded through the tenant filter.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, LoginInfo>> GetLoginInfoAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
