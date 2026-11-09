using DistroFlow.Application.Common;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Application.Interfaces.Persistence;
using DistroFlow.Domain.Constants;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.IntegrationTests.Persistence;

public class UnitOfWorkTests : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly ApiIntegrationTestFactory _factory;

    public UnitOfWorkTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unique_index_violation_is_translated_with_the_constraint_name()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var tenant = new Tenant { Name = $"Distributor {Guid.NewGuid():N}" };
        db.Tenants.Add(tenant);
        await unitOfWork.SaveChangesAsync();

        // Two employees of the same distributor with the same employee code.
        foreach (var name in new[] { "First", "Second" })
        {
            var login = await identity.CreateUserAsync(
                tenant.Id, $"uow-{Guid.NewGuid():N}@example.com", "Str0ng!Passw0rd", EmployeeRoles.SalesStaff);
            db.Employees.Add(new Employee
            {
                TenantId = tenant.Id,
                UserId = login.UserId,
                FullName = name,
                EmployeeCode = "EMP-1"
            });

            if (name == "First")
                await unitOfWork.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => unitOfWork.SaveChangesAsync());

        Assert.Equal(ConstraintNames.EmployeeCodePerTenant, ex.ConstraintName);
    }
}
