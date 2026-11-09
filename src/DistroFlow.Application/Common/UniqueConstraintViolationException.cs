namespace DistroFlow.Application.Common;

/// <summary>
/// The database rejected a save because a unique index would be violated.
/// Thrown by the persistence layer so the Application layer can react to specific rules
/// (e.g. duplicate employee code) without depending on EF Core or PostgreSQL types.
/// </summary>
public class UniqueConstraintViolationException(string constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string ConstraintName { get; } = constraintName;
}

/// <summary>Names of database unique indexes the Application layer reacts to.</summary>
public static class ConstraintNames
{
    /// <summary>Employee code is unique within one distributor.</summary>
    public const string EmployeeCodePerTenant = "IX_Employees_TenantId_EmployeeCode";
}
