using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DistroFlow.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _dbContext;

    public EmployeeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Employee employee)
    {
        _dbContext.Employees.Add(employee);
    }

    public async Task<Employee?> GetByUserIdReadOnlyAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken);
    }
}
