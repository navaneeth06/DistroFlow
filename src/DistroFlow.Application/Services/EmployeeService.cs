using DistroFlow.Application.Common;
using DistroFlow.Application.DTOs.Employees;
using DistroFlow.Application.Emails;
using DistroFlow.Application.Interfaces.Email;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Application.Interfaces.Persistence;
using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DistroFlow.Application.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEmailSender _emailSender;
    private readonly FrontendOptions _frontend;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        ITenantRepository tenantRepository,
        IIdentityService identityService,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IEmailSender emailSender,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<EmployeeService> logger)
    {
        _employeeRepository = employeeRepository;
        _tenantRepository = tenantRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _emailSender = emailSender;
        _frontend = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<InviteEmployeeResult> InviteAsync(
        InviteEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("Inviting an employee requires an authenticated user.");

        var tenant = await _tenantRepository.GetByIdReadOnlyAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Current tenant {tenantId} not found.");

        // 1. Login + employee + roles together: either all are saved or none.
        Employee employee;
        Guid userId;
        await using (var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            var login = await _identityService.CreateInvitedUserAsync(
                tenantId, request.Email, request.Roles, cancellationToken);

            if (login.Error == CreateUserError.EmailInUse)
                return InviteEmployeeResult.Failure(InviteEmployeeError.EmailInUse);

            if (!login.Succeeded)
                throw new InvalidOperationException($"Unexpected login creation error: {login.Error}");

            userId = login.UserId!.Value;
            employee = new Employee
            {
                TenantId = tenantId,
                UserId = userId,
                FullName = request.FullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                EmployeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
            _employeeRepository.Add(employee);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException ex)
                when (ex.ConstraintName == ConstraintNames.EmployeeCodePerTenant)
            {
                // Leaving without commit rolls back the login created above.
                return InviteEmployeeResult.Failure(InviteEmployeeError.EmployeeCodeInUse);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        // 2. Email after commit: the employee exists even if sending fails,
        //    and the Administrator can resend the invite.
        var emailSent = await TrySendInvitationAsync(employee, userId, request.Email, tenant.Name, cancellationToken);

        var logins = await _identityService.GetLoginInfoAsync([userId], cancellationToken);
        return InviteEmployeeResult.Success(new InviteEmployeeResponse
        {
            Employee = MapToResponse(employee, logins),
            InviteEmailSent = emailSent
        });
    }

    public async Task<UpdateEmployeeResult> UpdateAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        // Tenant filter applies: another distributor's employee is "not found".
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
            return UpdateEmployeeResult.Failure(UpdateEmployeeError.NotFound);

        // Checked here to return a clear 409; the domain method enforces it regardless.
        if (employee.Status == EmployeeStatus.Anonymised)
            return UpdateEmployeeResult.Failure(UpdateEmployeeError.NotEditable);

        employee.UpdateProfile(
            request.FullName.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim(),
            DateTime.UtcNow);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex)
            when (ex.ConstraintName == ConstraintNames.EmployeeCodePerTenant)
        {
            return UpdateEmployeeResult.Failure(UpdateEmployeeError.EmployeeCodeInUse);
        }

        var logins = await LoadLoginsAsync([employee], cancellationToken);
        return UpdateEmployeeResult.Success(MapToResponse(employee, logins));
    }

    public async Task<ChangeEmployeeRolesResult> ChangeRolesAsync(
        Guid employeeId,
        ChangeEmployeeRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("Changing roles requires an authenticated user.");

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // 1. Lock this distributor's row. A concurrent role change in the same distributor
        //    waits here until we commit, so the admin count below cannot be stale.
        await _tenantRepository.LockForUpdateAsync(tenantId, cancellationToken);

        // 2. Load the employee (tenant filter: another distributor's employee is "not found").
        var employee = await _employeeRepository.GetByIdReadOnlyAsync(employeeId, cancellationToken);
        if (employee is null)
            return ChangeEmployeeRolesResult.Failure(ChangeEmployeeRolesError.NotFound);

        if (employee.UserId is not { } userId)
            return ChangeEmployeeRolesResult.Failure(ChangeEmployeeRolesError.NotEditable);

        // 3. Check the rule: never remove the last active Administrator.
        var currentRoles = (await _identityService.GetLoginInfoAsync([userId], cancellationToken))[userId].Roles;
        var removesAdministrator =
            currentRoles.Contains(EmployeeRoles.Administrator) &&
            !request.Roles.Contains(EmployeeRoles.Administrator);

        if (removesAdministrator && employee.IsActive &&
            await _identityService.CountActiveAdministratorsAsync(tenantId, cancellationToken) <= 1)
        {
            return ChangeEmployeeRolesResult.Failure(ChangeEmployeeRolesError.LastAdministrator);
        }

        // 4. Apply and commit (which also releases the lock).
        await _identityService.ReplaceRolesAsync(userId, request.Roles, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var logins = await _identityService.GetLoginInfoAsync([userId], cancellationToken);
        return ChangeEmployeeRolesResult.Success(MapToResponse(employee, logins));
    }

    public async Task<EmployeeStatusChangeResult> DeactivateAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenant.TenantId
            ?? throw new InvalidOperationException("Deactivating an employee requires an authenticated user.");

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Same protection as role changes: concurrent admin-affecting changes in one
        // distributor run one at a time, so the admin count below is never stale.
        await _tenantRepository.LockForUpdateAsync(tenantId, cancellationToken);

        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
            return EmployeeStatusChangeResult.Failure(EmployeeStatusChangeError.NotFound);

        if (employee.Status == EmployeeStatus.Anonymised || employee.UserId is not { } userId)
            return EmployeeStatusChangeResult.Failure(EmployeeStatusChangeError.NotEditable);

        var logins = await _identityService.GetLoginInfoAsync([userId], cancellationToken);

        // Already deactivated: nothing to do (repeating the request is safe).
        if (employee.Status == EmployeeStatus.Deactivated)
            return EmployeeStatusChangeResult.Success(MapToResponse(employee, logins));

        if (logins[userId].Roles.Contains(EmployeeRoles.Administrator) &&
            await _identityService.CountActiveAdministratorsAsync(tenantId, cancellationToken) <= 1)
        {
            return EmployeeStatusChangeResult.Failure(EmployeeStatusChangeError.LastAdministrator);
        }

        // Employee status and login are switched off together, in this one transaction.
        employee.Deactivate(DateTime.UtcNow);
        await _identityService.SetLoginActiveAsync(userId, isActive: false, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return EmployeeStatusChangeResult.Success(MapToResponse(employee, logins));
    }

    public async Task<EmployeeStatusChangeResult> ReactivateAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        // No tenant lock needed: reactivating can only add administrators, never remove the
        // last one. The one race that matters (email taken meanwhile) is caught by the
        // database's unique index on active emails.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
            return EmployeeStatusChangeResult.Failure(EmployeeStatusChangeError.NotFound);

        if (employee.Status == EmployeeStatus.Anonymised || employee.UserId is not { } userId)
            return EmployeeStatusChangeResult.Failure(EmployeeStatusChangeError.NotEditable);

        if (employee.Status == EmployeeStatus.Active)
        {
            // Already active: nothing to do (repeating the request is safe).
            var current = await _identityService.GetLoginInfoAsync([userId], cancellationToken);
            return EmployeeStatusChangeResult.Success(MapToResponse(employee, current));
        }

        employee.Reactivate(DateTime.UtcNow);

        if (await _identityService.SetLoginActiveAsync(userId, isActive: true, cancellationToken)
            == SetLoginActiveResult.EmailInUse)
        {
            // Leaving without commit rolls back the employee change too.
            return EmployeeStatusChangeResult.Failure(EmployeeStatusChangeError.EmailInUse);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var logins = await _identityService.GetLoginInfoAsync([userId], cancellationToken);
        return EmployeeStatusChangeResult.Success(MapToResponse(employee, logins));
    }

    public async Task<ResendInviteResult> ResendInviteAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        // Tenant filter applies: another distributor's employee is "not found".
        var employee = await _employeeRepository.GetByIdReadOnlyAsync(employeeId, cancellationToken);
        if (employee is null)
            return ResendInviteResult.Failure(ResendInviteError.NotFound);

        if (!employee.IsActive || employee.UserId is not { } userId)
            return ResendInviteResult.Failure(ResendInviteError.NotActive);

        if (await _identityService.HasPasswordAsync(userId, cancellationToken))
            return ResendInviteResult.Failure(ResendInviteError.AlreadyAccepted);

        var tenant = await _tenantRepository.GetByIdReadOnlyAsync(employee.TenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {employee.TenantId} not found.");

        var logins = await _identityService.GetLoginInfoAsync([userId], cancellationToken);
        var email = logins[userId].Email
            ?? throw new InvalidOperationException($"Login {userId} has no email.");

        var sent = await TrySendInvitationAsync(employee, userId, email, tenant.Name, cancellationToken);
        return ResendInviteResult.Success(sent);
    }

    private async Task<bool> TrySendInvitationAsync(
        Employee employee,
        Guid userId,
        string email,
        string distributorName,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await _identityService.GeneratePasswordSetupTokenAsync(userId, cancellationToken);
            var link = $"{_frontend.BaseUrl.TrimEnd('/')}/set-password" +
                       $"?userId={userId}&token={Uri.EscapeDataString(token)}";

            await _emailSender.SendAsync(
                InvitationEmail.Create(email, employee.FullName, distributorName, link),
                cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Not swallowed: logged as an error and reported to the caller (InviteEmailSent = false).
            // The employee id is logged, not the email (personal data).
            _logger.LogError(ex, "Invitation email for employee {EmployeeId} could not be sent", employee.Id);
            return false;
        }
    }

    public async Task<IReadOnlyList<EmployeeResponse>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        // Tenant filter applies: only the caller's distributor's employees come back.
        var employees = await _employeeRepository.ListAsync(cancellationToken);

        var logins = await LoadLoginsAsync(employees, cancellationToken);

        return employees.Select(e => MapToResponse(e, logins)).ToList();
    }

    public async Task<EmployeeResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // Another distributor's id returns null too, so the API answers 404, never 403:
        // it must not even reveal that the id exists.
        var employee = await _employeeRepository.GetByIdReadOnlyAsync(id, cancellationToken);
        if (employee is null)
            return null;

        var logins = await LoadLoginsAsync([employee], cancellationToken);

        return MapToResponse(employee, logins);
    }

    private Task<IReadOnlyDictionary<Guid, LoginInfo>> LoadLoginsAsync(
        IReadOnlyCollection<Employee> employees,
        CancellationToken cancellationToken)
    {
        var userIds = employees
            .Where(e => e.UserId is not null)
            .Select(e => e.UserId!.Value)
            .ToList();

        return _identityService.GetLoginInfoAsync(userIds, cancellationToken);
    }

    private static EmployeeResponse MapToResponse(
        Employee employee,
        IReadOnlyDictionary<Guid, LoginInfo> logins)
    {
        // Anonymised employees have no login, so no email or roles.
        LoginInfo? login = null;
        if (employee.UserId is { } userId)
            logins.TryGetValue(userId, out login);

        return new EmployeeResponse
        {
            Id = employee.Id,
            FullName = employee.FullName,
            Email = login?.Email,
            Phone = employee.Phone,
            EmployeeCode = employee.EmployeeCode,
            Status = employee.Status.ToString(),
            Roles = login?.Roles ?? [],
            CreatedAtUtc = employee.CreatedAtUtc,
            UpdatedAtUtc = employee.UpdatedAtUtc
        };
    }
}
