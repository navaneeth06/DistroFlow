namespace DistroFlow.Application.DTOs.Employees;

/// <summary>Outcome of deactivating or reactivating an employee.</summary>
public enum EmployeeStatusChangeError
{
    None = 0,

    /// <summary>No such employee in the caller's distributor.</summary>
    NotFound = 1,

    /// <summary>The employee is anonymised; their status can no longer change.</summary>
    NotEditable = 2,

    /// <summary>Deactivating would leave the distributor without an active Administrator.</summary>
    LastAdministrator = 3,

    /// <summary>Reactivating is impossible: the email is now active at another distributor.</summary>
    EmailInUse = 4
}

public sealed record EmployeeStatusChangeResult
{
    public EmployeeResponse? Response { get; private init; }

    public EmployeeStatusChangeError Error { get; private init; }

    public bool Succeeded => Error == EmployeeStatusChangeError.None;

    public static EmployeeStatusChangeResult Success(EmployeeResponse response) => new() { Response = response };

    public static EmployeeStatusChangeResult Failure(EmployeeStatusChangeError error) => new() { Error = error };
}
