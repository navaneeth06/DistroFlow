namespace DistroFlow.Domain.Constants;

/// <summary>
/// The fixed set of employee roles. Authorization policies refer to these names,
/// and they are seeded into the database by a migration.
/// </summary>
public static class EmployeeRoles
{
    public const string Administrator = "Administrator";
    public const string SalesStaff = "Sales Staff";
    public const string DispatchStaff = "Dispatch Staff";
    public const string Approver = "Approver";
    public const string AccountsStaff = "Accounts Staff";

    public static IReadOnlyList<string> All { get; } =
    [
        Administrator,
        SalesStaff,
        DispatchStaff,
        Approver,
        AccountsStaff
    ];
}
