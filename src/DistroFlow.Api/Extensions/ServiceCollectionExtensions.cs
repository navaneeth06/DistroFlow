using System.Text;
using DistroFlow.Application.Common;
using DistroFlow.Application.DTOs.Common;
using DistroFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DistroFlow.Api.Extensions;

/// <summary>HTTP-layer registrations: controllers and how incoming requests are authenticated.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                // Return DTO validation failures ([Required], [EmailAddress], ...) in the ApiResponse shape.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .ToDictionary(
                            x => x.Key,
                            x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                    return new BadRequestObjectResult(
                        ApiResponse<object>.FailureResponse("Validation failed.", errors));
                };
            });

        return services;
    }

    /// <summary>
    /// Validates incoming bearer tokens with the same validated <see cref="JwtOptions"/>
    /// used to issue them, so issuing and validation can never drift apart.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep claim names exactly as issued ("sub", "role", "tenant_id") instead of
                // remapping them to long XML-namespace URIs.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    // Only accept the algorithm we sign with (blocks algorithm-confusion attacks).
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    // Default tolerance is 5 minutes; 30 seconds is enough for clock drift.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = AppClaimTypes.UserId,
                    RoleClaimType = AppClaimTypes.Role
                };
            });

        services.AddAuthorization();

        return services;
    }
}
