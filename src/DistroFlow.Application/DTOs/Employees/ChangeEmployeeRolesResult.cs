namespace DistroFlow.Application.DTOs.Employees;

public enum ChangeEmployeeRolesError
{
    None = 0,

    /// <summary>No such employee in the caller's distributor.</summary>
    NotFound = 1,

    /// <summary>The employee has no login (anonymised), so there are no roles to change.</summary>
    NotEditable = 2,

    /// <summary>The change would leave the distributor without an active Administrator.</summary>
    LastAdministrator = 3
}

public sealed record ChangeEmployeeRolesResult
{
    public EmployeeResponse? Response { get; private init; }

    public ChangeEmployeeRolesError Error { get; private init; }

    public bool Succeeded => Error == ChangeEmployeeRolesError.None;

    public static ChangeEmployeeRolesResult Success(EmployeeResponse response) => new() { Response = response };

    public static ChangeEmployeeRolesResult Failure(ChangeEmployeeRolesError error) => new() { Error = error };
}
