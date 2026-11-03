using DistroFlow.Application.Interfaces.Services;
using DistroFlow.Application.DTOs.Common;
using Microsoft.AspNetCore.Mvc;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors
                        .Select(e => e.ErrorMessage)
                        .ToArray()
                );

            var response = ApiResponse<object>.FailureResponse(
                "Validation failed.",
                errors
            );

            return new BadRequestObjectResult(response);
        };
    });

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string 'DefaultConnection' is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<ITenantService, TenantService>();

var app = builder.Build();

app.MapGet("/health/database", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    return canConnect
        ? Results.Ok(new
        {
            status = "Connected",
            database = "distroflow_dev"
        })
        : Results.Problem("Unable to connect to PostgreSQL.");
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
public partial class Program { }