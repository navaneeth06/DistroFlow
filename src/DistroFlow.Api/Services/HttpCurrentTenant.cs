using DistroFlow.Application.Common;
using DistroFlow.Application.Interfaces.Identity;

namespace DistroFlow.Api.Services;

public class HttpCurrentTenant(IHttpContextAccessor httpContextAccessor): ICurrentTenant
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Guid? TenantId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst(AppClaimTypes.TenantId)?.Value;
            return Guid.TryParse(value ,out var tenantId) ? tenantId : null;
        }
    }
}