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
    /// Creates a login with no password (an invited employee sets it via an emailed link)
    /// and assigns the roles. Saves immediately; wrap in a transaction with the employee row.
    /// </summary>
    Task<CreateUserResult> CreateInvitedUserAsync(
        Guid tenantId,
        string email,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A signed, single-use, expiring token that lets the user set their password.
    /// </summary>
    Task<string> GeneratePasswordSetupTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// An invited user sets their first password using the token from their invite email.
    /// Never works for an account that already has a password (this is not a password reset).
    /// </summary>
    Task<SetPasswordResult> SetInitialPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Makes the user's roles exactly <paramref name="roles"/> (adds missing, removes others).</summary>
    Task ReplaceRolesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>Number of active logins in the tenant that have the Administrator role.</summary>
    Task<int> CountActiveAdministratorsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>False for an invited user who has not set their password yet.</summary>
    Task<bool> HasPasswordAsync(
        Guid userId,
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
