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

    /// <summary>An invited employee sets their first password using the link from their invite email.</summary>
    [AllowAnonymous]
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(
        [FromBody] SetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.SetPasswordAsync(request, cancellationToken);

        return result.Error switch
        {
            SetPasswordError.None => Ok(
                ApiResponse<object>.SuccessResponse(null, "Password set. You can now log in.")),

            SetPasswordError.InvalidLink => BadRequest(
                ApiResponse<object>.FailureResponse(
                    "This link is invalid or has expired. Ask your administrator to resend the invite.")),

            SetPasswordError.PasswordRejected => BadRequest(
                ApiResponse<object>.FailureResponse(
                    "Validation failed.",
                    new Dictionary<string, string[]>
                    {
                        [nameof(SetPasswordRequest.NewPassword)] = result.Messages.ToArray()
                    })),

            _ => throw new InvalidOperationException($"Unhandled set-password error: {result.Error}")
        };
    }

    /// <summary>Exchanges email + password for a JWT access token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

        return result.Error switch
        {
            LoginError.None => Ok(
                ApiResponse<LoginResponse>.SuccessResponse(result.Response, "Login successful.")),

            LoginError.InvalidCredentials => Unauthorized(
                ApiResponse<object>.FailureResponse("Invalid email or password.")),

            LoginError.TenantSuspended => StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.FailureResponse(
                    "This distributor account is suspended. Contact support.")),

            _ => throw new InvalidOperationException($"Unhandled login error: {result.Error}")
        };
    }
}
