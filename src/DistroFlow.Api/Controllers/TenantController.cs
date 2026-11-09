using DistroFlow.Application.DTOs.Common;
using DistroFlow.Application.DTOs.Tenants;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DistroFlow.Api.Controllers;

/// <summary>
/// The caller's own distributor. There is no id in the route: the tenant comes from the
/// signed token, so a distributor cannot even address another distributor's record.
/// Tenants are created via POST /api/v1/auth/register.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/tenant")]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    /// <summary>Any employee may read their distributor's settings (currency, time zone...).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.GetCurrentAsync(cancellationToken);

        if (tenant is null)
        {
            return NotFound(ApiResponse<TenantResponse>.FailureResponse("Tenant not found."));
        }

        return Ok(ApiResponse<TenantResponse>.SuccessResponse(tenant, "Tenant retrieved successfully."));
    }

    /// <summary>Only an Administrator may change the distributor's settings.</summary>
    [HttpPut]
    [Authorize(Roles = EmployeeRoles.Administrator)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.UpdateCurrentAsync(request, cancellationToken);

        if (tenant is null)
        {
            return NotFound(ApiResponse<TenantResponse>.FailureResponse("Tenant not found."));
        }

        return Ok(ApiResponse<TenantResponse>.SuccessResponse(tenant, "Tenant updated successfully."));
    }
}
