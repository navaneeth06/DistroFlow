namespace DistroFlow.Application.DTOs.Employees;

public enum InviteEmployeeError
{
    None = 0,

    /// <summary>The email already belongs to an active account (in any distributor).</summary>
    EmailInUse = 1,

    /// <summary>The employee code is already used by another employee of this distributor.</summary>
    EmployeeCodeInUse = 2
}

public class InviteEmployeeResponse
{
    public EmployeeResponse Employee { get; set; } = new();

    /// <summary>
    /// False if the employee was created but the invite email could not be sent;
    /// the Administrator can resend the invite.
    /// </summary>
    public bool InviteEmailSent { get; set; }
}

public sealed record InviteEmployeeResult
{
    public InviteEmployeeResponse? Response { get; private init; }

    public InviteEmployeeError Error { get; private init; }

    public bool Succeeded => Error == InviteEmployeeError.None;

    public static InviteEmployeeResult Success(InviteEmployeeResponse response) => new() { Response = response };

    public static InviteEmployeeResult Failure(InviteEmployeeError error) => new() { Error = error };
}
