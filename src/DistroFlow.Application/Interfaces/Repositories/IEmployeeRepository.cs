using DistroFlow.Domain.Entities;

namespace DistroFlow.Application.Interfaces.Repositories;

public interface IEmployeeRepository
{
    void Add(Employee employee);
}
