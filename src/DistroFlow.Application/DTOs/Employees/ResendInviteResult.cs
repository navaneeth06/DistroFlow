namespace DistroFlow.Application.DTOs.Employees;

public enum ResendInviteError
{
    None = 0,

    /// <summary>No such employee in the caller's distributor.</summary>
    NotFound = 1,

    /// <summary>The employee already set a password; there is no pending invite.</summary>
    AlreadyAccepted = 2,

    /// <summary>Deactivated or anonymised employees cannot be invited.</summary>
    NotActive = 3
}

public class ResendInviteResponse
{
    public bool InviteEmailSent { get; set; }
}

public sealed record ResendInviteResult
{
    public ResendInviteResponse? Response { get; private init; }

    public ResendInviteError Error { get; private init; }

    public bool Succeeded => Error == ResendInviteError.None;

    public static ResendInviteResult Success(bool emailSent) =>
        new() { Response = new ResendInviteResponse { InviteEmailSent = emailSent } };

    public static ResendInviteResult Failure(ResendInviteError error) => new() { Error = error };
}
