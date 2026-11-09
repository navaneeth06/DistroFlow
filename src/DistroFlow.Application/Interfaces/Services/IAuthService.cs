using DistroFlow.Application.DTOs.Auth;
using DistroFlow.Application.Interfaces.Identity;

namespace DistroFlow.Application.Interfaces.Services;

public interface IAuthService
{
    Task<RegisterDistributorResult> RegisterDistributorAsync(
        RegisterDistributorRequest request,
        CancellationToken cancellationToken = default);

    Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<SetPasswordResult> SetPasswordAsync(
        SetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
