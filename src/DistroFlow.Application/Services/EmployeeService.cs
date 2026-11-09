using DistroFlow.Application.DTOs.Employees;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IIdentityService _identityService;

    public EmployeeService(IEmployeeRepository employeeRepository, IIdentityService identityService)
    {
        _employeeRepository = employeeRepository;
        _identityService = identityService;
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
