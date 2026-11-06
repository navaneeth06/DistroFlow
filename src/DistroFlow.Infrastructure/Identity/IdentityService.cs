using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DistroFlow.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private const string ActiveEmailIndex = "EmailIndex";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        AppDbContext dbContext,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CreateUserResult> CreateUserAsync(
        Guid tenantId,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            TenantId = tenantId,
            Email = email.Trim()
        };

        // Email is the login; UserName is internal. Using the id keeps Identity's
        // global UserName uniqueness from blocking email reuse across tenants.
        user.UserName = user.Id.ToString();

        IdentityResult created;
        try
        {
            created = await _userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException ex) when (IsActiveEmailViolation(ex))
        {
            // The database is the source of truth for "one active account per email",
            // so this also covers two registrations racing with the same email.
            // Email is not logged (personal data).
            _logger.LogWarning(
                "Login creation rejected for tenant {TenantId}: email already active on another account",
                tenantId);

            // The failed insert is still tracked; detach it so a later SaveChanges
            // on this request's DbContext does not retry it.
            _dbContext.Entry(user).State = EntityState.Detached;

            return CreateUserResult.Failure(
                CreateUserError.EmailInUse,
                ["This email can't be used right now."]);
        }

        if (!created.Succeeded)
        {
            return CreateUserResult.Failure(
                CreateUserError.ValidationFailed,
                created.Errors.Select(e => e.Description).ToList());
        }

        var assigned = await _userManager.AddToRoleAsync(user, role);
        if (!assigned.Succeeded)
        {
            // Roles are seeded by migration; a missing role is a bug, not user error.
            throw new InvalidOperationException(
                $"Could not assign role '{role}': {string.Join("; ", assigned.Errors.Select(e => e.Description))}");
        }

        return CreateUserResult.Success(user.Id);
    }

    public async Task<CredentialCheckResult> CheckCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = _userManager.NormalizeEmail(email.Trim());

        // The partial unique index guarantees at most one active account per email,
        // so login needs no "which distributor?" input.
        var user = await _userManager.Users.SingleOrDefaultAsync(
            u => u.NormalizedEmail == normalizedEmail && u.IsActive,
            cancellationToken);

        if (user is null)
        {
            // Hash anyway so "unknown email" takes about as long as "wrong password";
            // otherwise response time reveals which emails have accounts.
            _userManager.PasswordHasher.HashPassword(new ApplicationUser(), password);
            return CredentialCheckResult.Failure(CredentialCheckError.InvalidCredentials);
        }

        // Checked before the password so a locked account cannot keep being brute-forced.
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login rejected for locked-out user {UserId}", user.Id);
            return CredentialCheckResult.Failure(CredentialCheckError.LockedOut);
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            // Increments the failed count and locks the account once it reaches the limit.
            await _userManager.AccessFailedAsync(user);
            return CredentialCheckResult.Failure(CredentialCheckError.InvalidCredentials);
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return CredentialCheckResult.Success(user.Id, user.TenantId, roles.ToList());
    }

    private static bool IsActiveEmailViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ActiveEmailIndex
        };
}
