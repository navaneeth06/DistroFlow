namespace DistroFlow.Application.Interfaces.Identity;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
}