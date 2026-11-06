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
}
