using DistroFlow.Application.DTOs.Auth;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Application.Interfaces.Persistence;
using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Services;

public class AuthService : IAuthService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IIdentityService _identityService;
    private readonly IUnitOfWork _unitOfWork;

    public AuthService(
        ITenantRepository tenantRepository,
        IEmployeeRepository employeeRepository,
        IIdentityService identityService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _employeeRepository = employeeRepository;
        _identityService = identityService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Creates tenant → login → employee in one transaction, so a failure at any step
    /// (e.g. email already in use) leaves no half-registered distributor behind.
    /// </summary>
    public async Task<RegisterDistributorResult> RegisterDistributorAsync(
        RegisterDistributorRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var tenant = new Tenant
        {
            Name = request.BusinessName.Trim(),
            TimeZoneId = request.TimeZoneId.Trim(),
            CreatedAtUtc = now
        };
        _tenantRepository.Add(tenant);

        // The login row has a foreign key to the tenant, so the tenant is saved first.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var login = await _identityService.CreateUserAsync(
            tenant.Id,
            request.Email,
            request.Password,
            EmployeeRoles.Administrator,
            cancellationToken);

        if (!login.Succeeded)
        {
            // Returning without commit: disposing the transaction rolls back the tenant insert.
            return RegisterDistributorResult.Failure(login.Error, login.Messages);
        }

        var employee = new Employee
        {
            TenantId = tenant.Id,
            UserId = login.UserId,
            FullName = request.OwnerFullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.OwnerPhone) ? null : request.OwnerPhone.Trim(),
            CreatedAtUtc = now
        };
        _employeeRepository.Add(employee);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RegisterDistributorResult.Success(new RegisterDistributorResponse
        {
            TenantId = tenant.Id,
            EmployeeId = employee.Id,
            UserId = login.UserId!.Value
        });
    }
}
