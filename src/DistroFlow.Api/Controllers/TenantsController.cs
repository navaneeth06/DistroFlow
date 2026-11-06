using DistroFlow.Application.DTOs.Common;
using DistroFlow.Application.DTOs.Tenants;
using DistroFlow.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace DistroFlow.Api.Controllers;

[ApiController]
[Route("api/v1/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    // Tenants are created only through POST /api/v1/auth/register, together with their owner.
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var tenants = await _tenantService.GetAllAsync(cancellationToken);

        return Ok(
            ApiResponse<IReadOnlyList<TenantResponse>>.SuccessResponse(
                tenants,
                "Tenants retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.GetByIdAsync(
            id, cancellationToken);

        if (tenant is null)
        {
            return NotFound(
                ApiResponse<TenantResponse>.FailureResponse(
                    "Tenant not found."));
        }

        return Ok(
            ApiResponse<TenantResponse>.SuccessResponse(
                tenant,
                "Tenant retrieved successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantService.UpdateAsync(
            id, request, cancellationToken);

        if (tenant is null)
        {
            return NotFound(
                ApiResponse<TenantResponse>.FailureResponse(
                    "Tenant not found."));
        }

        return Ok(
            ApiResponse<TenantResponse>.SuccessResponse(
                tenant,
                "Tenant updated successfully."));
    }

    [HttpPatch("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deactivated = await _tenantService.DeactivateAsync(
            id, cancellationToken);

        if (!deactivated)
        {
            return NotFound(
                ApiResponse<object>.FailureResponse(
                    "Tenant not found."));
        }

        return Ok(
            ApiResponse<object>.SuccessResponse(
                null,
                "Tenant deactivated successfully."));
    }
}