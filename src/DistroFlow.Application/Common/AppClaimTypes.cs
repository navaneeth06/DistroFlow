namespace DistroFlow.Application.Common;

/// <summary>
/// Claim names in DistroFlow access tokens. Short JWT-style names are used as-is
/// (inbound claim mapping is disabled), so what the token contains is what the code reads.
/// </summary>
public static class AppClaimTypes
{
    /// <summary>Login account id (standard JWT "sub").</summary>
    public const string UserId = "sub";

    public const string EmployeeId = "employee_id";

    /// <summary>The only trusted source of the current tenant for a request.</summary>
    public const string TenantId = "tenant_id";

    public const string Role = "role";
}
