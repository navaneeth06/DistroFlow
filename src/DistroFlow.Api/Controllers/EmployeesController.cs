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

    /// <summary>Adds an employee and emails them a link to set their password.</summary>
    [HttpPost]
    public async Task<IActionResult> Invite(
        [FromBody] InviteEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.InviteAsync(request, cancellationToken);

        return result.Error switch
        {
            InviteEmployeeError.None => StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<InviteEmployeeResponse>.SuccessResponse(
                    result.Response,
                    result.Response!.InviteEmailSent
                        ? "Employee invited. A set-password email has been sent."
                        : "Employee added, but the invite email could not be sent. Try resending the invite.")),

            InviteEmployeeError.EmailInUse => Conflict(
                ApiResponse<object>.FailureResponse(
                    "This email can't be used right now. If the person worked for another distributor, " +
                    "ask them to check with their previous employer.")),

            InviteEmployeeError.EmployeeCodeInUse => Conflict(
                ApiResponse<object>.FailureResponse("Another employee already has this employee code.")),

            _ => throw new InvalidOperationException($"Unhandled invite error: {result.Error}")
        };
    }
}
