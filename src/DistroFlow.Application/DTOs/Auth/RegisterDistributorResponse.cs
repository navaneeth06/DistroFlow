namespace DistroFlow.Application.DTOs.Auth;

public class RegisterDistributorResponse
{
    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid UserId { get; set; }
}
