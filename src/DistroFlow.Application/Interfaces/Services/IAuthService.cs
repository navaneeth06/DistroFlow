using DistroFlow.Application.DTOs.Auth;

namespace DistroFlow.Application.Interfaces.Services;

public interface IAuthService
{
    Task<RegisterDistributorResult> RegisterDistributorAsync(
        RegisterDistributorRequest request,
        CancellationToken cancellationToken = default);

    Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);
}
