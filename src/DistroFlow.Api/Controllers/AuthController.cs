using DistroFlow.Application.DTOs.Auth;
using DistroFlow.Application.DTOs.Common;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DistroFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Self-signup for a new distributor and its owner (first Administrator).</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDistributorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterDistributorAsync(request, cancellationToken);

        return result.Error switch
        {
            CreateUserError.None => StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<RegisterDistributorResponse>.SuccessResponse(
                    result.Response,
                    "Distributor registered successfully.")),

            CreateUserError.EmailInUse => Conflict(
                ApiResponse<object>.FailureResponse(result.Messages[0])),

            CreateUserError.ValidationFailed => BadRequest(
                ApiResponse<object>.FailureResponse(
                    "Validation failed.",
                    new Dictionary<string, string[]>
                    {
                        [nameof(RegisterDistributorRequest.Password)] = result.Messages.ToArray()
                    })),

            _ => throw new InvalidOperationException($"Unhandled registration error: {result.Error}")
        };
    }
}
