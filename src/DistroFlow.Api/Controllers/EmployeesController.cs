using DistroFlow.Application.DTOs.Common;
using DistroFlow.Application.DTOs.Employees;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DistroFlow.Api.Controllers;

/// <summary>Employee management for the caller's own distributor. Administrators only.</summary>
[ApiController]
[Authorize(Roles = EmployeeRoles.Administrator)]
[Route("api/v1/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var employees = await _employeeService.ListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<EmployeeResponse>>.SuccessResponse(
            employees, "Employees retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.GetByIdAsync(id, cancellationToken);

        if (employee is null)
        {
            return NotFound(ApiResponse<EmployeeResponse>.FailureResponse("Employee not found."));
        }

        return Ok(ApiResponse<EmployeeResponse>.SuccessResponse(
            employee, "Employee retrieved successfully."));
    }
}
