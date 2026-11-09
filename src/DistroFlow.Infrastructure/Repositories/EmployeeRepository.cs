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

    public async Task<IReadOnlyList<Employee>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<Employee?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Employee?> GetByIdReadOnlyAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<Employee?> GetForLoginByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees
            // At login nobody is authenticated yet, so there is no current tenant and the
            // tenant filter would hide every row. Safe here: the user id comes from a
            // successful password check, and the result is never returned to the caller.
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken);
    }
}
