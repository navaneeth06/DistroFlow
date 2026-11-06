using DistroFlow.Application.Common;
using DistroFlow.Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DistroFlow.Api.Controllers;

/// <summary>
/// The signed-in employee. For now returns what the token says; profile data
/// (name, phone) is added with employee management.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me")]
public class MeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var me = new
        {
            UserId = Guid.Parse(User.FindFirst(AppClaimTypes.UserId)!.Value),
            EmployeeId = Guid.Parse(User.FindFirst(AppClaimTypes.EmployeeId)!.Value),
            TenantId = Guid.Parse(User.FindFirst(AppClaimTypes.TenantId)!.Value),
            Roles = User.FindAll(AppClaimTypes.Role).Select(c => c.Value).ToArray()
        };

        return Ok(ApiResponse<object>.SuccessResponse(me));
    }
}
