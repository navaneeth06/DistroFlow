namespace DistroFlow.Application.Interfaces.Identity;

public enum SetPasswordError
{
    None = 0,

    /// <summary>
    /// Unknown user, inactive account, password already set, or a used/expired/tampered token.
    /// Deliberately one error, so the response reveals nothing about which case it was.
    /// </summary>
    InvalidLink = 1,

    /// <summary>The password does not meet the policy. The link stays valid.</summary>
    PasswordRejected = 2
}

public sealed record SetPasswordResult
{
    public SetPasswordError Error { get; private init; }

    public IReadOnlyList<string> Messages { get; private init; } = [];

    public bool Succeeded => Error == SetPasswordError.None;

    public static SetPasswordResult Success() => new();

    public static SetPasswordResult Failure(SetPasswordError error, IReadOnlyList<string>? messages = null) =>
        new() { Error = error, Messages = messages ?? [] };
}
