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

    /// <summary>Changes an employee's name, phone and employee code.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.UpdateAsync(id, request, cancellationToken);

        return result.Error switch
        {
            UpdateEmployeeError.None => Ok(
                ApiResponse<EmployeeResponse>.SuccessResponse(result.Response, "Employee updated successfully.")),

            UpdateEmployeeError.NotFound => NotFound(
                ApiResponse<object>.FailureResponse("Employee not found.")),

            UpdateEmployeeError.NotEditable => Conflict(
                ApiResponse<object>.FailureResponse("This employee's personal data was erased and cannot be edited.")),

            UpdateEmployeeError.EmployeeCodeInUse => Conflict(
                ApiResponse<object>.FailureResponse("Another employee already has this employee code.")),

            _ => throw new InvalidOperationException($"Unhandled update error: {result.Error}")
        };
    }

    /// <summary>Replaces an employee's roles. The last active Administrator cannot be demoted.</summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> ChangeRoles(
        Guid id,
        [FromBody] ChangeEmployeeRolesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.ChangeRolesAsync(id, request, cancellationToken);

        return result.Error switch
        {
            ChangeEmployeeRolesError.None => Ok(
                ApiResponse<EmployeeResponse>.SuccessResponse(result.Response, "Roles updated successfully.")),

            ChangeEmployeeRolesError.NotFound => NotFound(
                ApiResponse<object>.FailureResponse("Employee not found.")),

            ChangeEmployeeRolesError.NotEditable => Conflict(
                ApiResponse<object>.FailureResponse("This employee has no login, so roles cannot be changed.")),

            ChangeEmployeeRolesError.LastAdministrator => Conflict(
                ApiResponse<object>.FailureResponse(
                    "This is the last active Administrator. Make someone else an Administrator first.")),

            _ => throw new InvalidOperationException($"Unhandled change-roles error: {result.Error}")
        };
    }

    /// <summary>Sends a new set-password link to an employee who has not accepted their invite yet.</summary>
    [HttpPost("{id:guid}/resend-invite")]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken cancellationToken)
    {
        var result = await _employeeService.ResendInviteAsync(id, cancellationToken);

        return result.Error switch
        {
            ResendInviteError.None => Ok(
                ApiResponse<ResendInviteResponse>.SuccessResponse(
                    result.Response,
                    result.Response!.InviteEmailSent
                        ? "Invite email sent."
                        : "The invite email could not be sent. Please try again later.")),

            ResendInviteError.NotFound => NotFound(
                ApiResponse<object>.FailureResponse("Employee not found.")),

            ResendInviteError.AlreadyAccepted => Conflict(
                ApiResponse<object>.FailureResponse("This employee has already set a password.")),

            ResendInviteError.NotActive => Conflict(
                ApiResponse<object>.FailureResponse("Only active employees can be invited.")),

            _ => throw new InvalidOperationException($"Unhandled resend-invite error: {result.Error}")
        };
    }
}
