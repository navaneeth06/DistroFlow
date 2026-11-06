namespace DistroFlow.Application.Interfaces.Identity;

public enum CreateUserError
{
    None = 0,

    /// <summary>The email already belongs to an active account (in any tenant).</summary>
    EmailInUse = 1,

    /// <summary>Identity rejected the input, e.g. the password does not meet the policy.</summary>
    ValidationFailed = 2
}

/// <summary>
/// Outcome of creating a login. Expected failures (email in use, weak password) are results,
/// not exceptions, so callers can map them to 409/400 without try/catch.
/// </summary>
public sealed record CreateUserResult
{
    public Guid? UserId { get; private init; }

    public CreateUserError Error { get; private init; }

    public IReadOnlyList<string> Messages { get; private init; } = [];

    public bool Succeeded => Error == CreateUserError.None;

    public static CreateUserResult Success(Guid userId) => new() { UserId = userId };

    public static CreateUserResult Failure(CreateUserError error, IReadOnlyList<string> messages) =>
        new() { Error = error, Messages = messages };
}
