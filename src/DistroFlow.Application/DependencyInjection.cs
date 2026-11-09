using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.Application;

public static class DependencyInjection
{
    /// <summary>Registers the Application layer's use-case services.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantService, TenantService>();

        return services;
    }
}
