namespace DistroFlow.Application.DTOs.Auth;

public enum LoginError
{
    None = 0,

    /// <summary>
    /// Unknown email, wrong password, inactive or locked account. Deliberately one error,
    /// so the response never reveals whether an email has an account.
    /// </summary>
    InvalidCredentials = 1,

    /// <summary>Correct credentials, but the distributor is suspended.</summary>
    TenantSuspended = 2
}

public sealed record LoginResult
{
    public LoginResponse? Response { get; private init; }

    public LoginError Error { get; private init; }

    public bool Succeeded => Error == LoginError.None;

    public static LoginResult Success(LoginResponse response) => new() { Response = response };

    public static LoginResult Failure(LoginError error) => new() { Error = error };
}
