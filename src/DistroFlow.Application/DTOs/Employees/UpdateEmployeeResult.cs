namespace DistroFlow.Application.DTOs.Employees;

public enum UpdateEmployeeError
{
    None = 0,

    /// <summary>No such employee in the caller's distributor.</summary>
    NotFound = 1,

    /// <summary>The employee is anonymised; their data must not be re-entered.</summary>
    NotEditable = 2,

    /// <summary>Another employee of this distributor already has this employee code.</summary>
    EmployeeCodeInUse = 3
}

public sealed record UpdateEmployeeResult
{
    public EmployeeResponse? Response { get; private init; }

    public UpdateEmployeeError Error { get; private init; }

    public bool Succeeded => Error == UpdateEmployeeError.None;

    public static UpdateEmployeeResult Success(EmployeeResponse response) => new() { Response = response };

    public static UpdateEmployeeResult Failure(UpdateEmployeeError error) => new() { Error = error };
}
