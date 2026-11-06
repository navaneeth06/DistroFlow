using DistroFlow.Application.Interfaces.Identity;

namespace DistroFlow.Application.DTOs.Auth;

/// <summary>
/// Registration can only fail on the login part (email in use, weak password);
/// other input is validated at the API boundary. So it reuses <see cref="CreateUserError"/>.
/// </summary>
public sealed record RegisterDistributorResult
{
    public RegisterDistributorResponse? Response { get; private init; }

    public CreateUserError Error { get; private init; }

    public IReadOnlyList<string> Messages { get; private init; } = [];

    public bool Succeeded => Error == CreateUserError.None;

    public static RegisterDistributorResult Success(RegisterDistributorResponse response) =>
        new() { Response = response };

    public static RegisterDistributorResult Failure(CreateUserError error, IReadOnlyList<string> messages) =>
        new() { Error = error, Messages = messages };
}
