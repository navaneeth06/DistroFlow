using DistroFlow.Application.Interfaces.Email;
using DistroFlow.Application.Interfaces.Identity;
using DistroFlow.Infrastructure.Email;
using DistroFlow.Application.Interfaces.Persistence;
using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Infrastructure.Identity;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DistroFlow.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything that talks to the outside world: database, Identity, token issuing.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddIdentityServices();
        services.AddTokenIssuing(configuration);
        services.AddEmail(configuration);

        return services;
    }

    private static void AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IEmailSender, SmtpEmailSender>();
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string 'DefaultConnection' is missing.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
    }

    private static void AddIdentityServices(this IServiceCollection services)
    {
        // UserManager/RoleManager backed by EF Core. AddIdentityCore (not AddIdentity)
        // skips cookie/UI defaults; the API authenticates with JWT bearer tokens instead.
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Password policy for every path that sets a password (register, invite, change).
                options.Password.RequiredLength = 8;

                // Email is the login. Uniqueness among *active* accounts is enforced by a
                // partial unique index in the database, so Identity's global check stays off.
                options.User.RequireUniqueEmail = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
    }

    private static void AddTokenIssuing(this IServiceCollection services, IConfiguration configuration)
    {
        // The signing key is a secret: user-secrets in dev, env var Jwt__SigningKey in prod.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();
    }
}
