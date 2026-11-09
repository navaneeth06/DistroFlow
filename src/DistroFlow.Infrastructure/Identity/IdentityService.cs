using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Domain.Constants;
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

    public Task<CreateUserResult> CreateUserAsync(
        Guid tenantId,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default) =>
        CreateAsync(tenantId, email, password, [role]);

    public Task<CreateUserResult> CreateInvitedUserAsync(
        Guid tenantId,
        string email,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default) =>
        CreateAsync(tenantId, email, password: null, roles);

    public async Task<string> GeneratePasswordSetupTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");

        // Identity's password-reset token: signed with Data Protection, tied to the user's
        // security stamp (so it stops working once the password is set) and time-limited.
        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<SetPasswordResult> SetInitialPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        // Same answer for every "this link can't be used" case.
        if (user is null || !user.IsActive || await _userManager.HasPasswordAsync(user))
        {
            return SetPasswordResult.Failure(SetPasswordError.InvalidLink);
        }

        // Verifies the token (signature, expiry, security stamp) and then the password policy.
        // On success the security stamp changes, so this token, and any other outstanding
        // invite link for this user, stops working.
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            var invalidToken = result.Errors.Any(e => e.Code == _userManager.ErrorDescriber.InvalidToken().Code);

            return invalidToken
                ? SetPasswordResult.Failure(SetPasswordError.InvalidLink)
                : SetPasswordResult.Failure(
                    SetPasswordError.PasswordRejected,
                    result.Errors.Select(e => e.Description).ToList());
        }

        // Using the emailed link proves the employee controls this inbox.
        user.EmailConfirmed = true;
        var updated = await _userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not confirm email for user {user.Id}: {string.Join("; ", updated.Errors.Select(e => e.Description))}");
        }

        return SetPasswordResult.Success();
    }

    public async Task ReplaceRolesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");

        var current = await _userManager.GetRolesAsync(user);
        var toRemove = current.Except(roles).ToList();
        var toAdd = roles.Except(current).ToList();

        if (toRemove.Count > 0)
        {
            EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, toRemove), "remove roles from", userId);
        }

        if (toAdd.Count > 0)
        {
            EnsureSucceeded(await _userManager.AddToRolesAsync(user, toAdd), "add roles to", userId);
        }
    }

    public async Task<int> CountActiveAdministratorsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var administrator = _userManager.NormalizeName(EmployeeRoles.Administrator);

        // Login accounts have no tenant query filter (see CLAUDE.md), so filter by tenant explicitly.
        return await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles on userRole.RoleId equals role.Id
                join user in _dbContext.Users on userRole.UserId equals user.Id
                where user.TenantId == tenantId
                      && user.IsActive
                      && role.NormalizedName == administrator
                select user.Id)
            .CountAsync(cancellationToken);
    }

    /// <summary>Identity reports failures as results; for these calls a failure is a bug.</summary>
    private static void EnsureSucceeded(IdentityResult result, string action, Guid userId)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not {action} user {userId}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    public async Task<bool> HasPasswordAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");

        return await _userManager.HasPasswordAsync(user);
    }

    /// <param name="password">Null for an invited user, who sets it later via the emailed link.</param>
    private async Task<CreateUserResult> CreateAsync(
        Guid tenantId,
        string email,
        string? password,
        IReadOnlyCollection<string> roles)
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
            created = password is null
                ? await _userManager.CreateAsync(user)
                : await _userManager.CreateAsync(user, password);
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

        var assigned = await _userManager.AddToRolesAsync(user, roles);
        if (!assigned.Succeeded)
        {
            // Roles are validated at the API boundary and seeded by migration;
            // failing here is a bug, not user error.
            throw new InvalidOperationException(
                $"Could not assign roles '{string.Join(", ", roles)}': {string.Join("; ", assigned.Errors.Select(e => e.Description))}");
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

    public async Task<IReadOnlyDictionary<Guid, LoginInfo>> GetLoginInfoAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, LoginInfo>();
        }

        // Two queries in total, however many employees: emails, then all their role names.
        var emails = await _dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        var roles = await (
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);

        var rolesByUser = roles
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Name!).Order().ToList());

        return emails.ToDictionary(
            u => u.Id,
            u => new LoginInfo(u.Email, rolesByUser.GetValueOrDefault(u.Id, [])));
    }

    private static bool IsActiveEmailViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ActiveEmailIndex
        };
}
