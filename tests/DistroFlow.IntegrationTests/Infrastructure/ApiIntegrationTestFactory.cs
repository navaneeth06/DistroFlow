
using DistroFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DistroFlow.IntegrationTests.Infrastructure;

public class ApiIntegrationTestFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:18")
            .WithDatabase("distroflow_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var client = CreateClient();

        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    /// <summary>Test-only key; real keys come from user-secrets or environment variables.</summary>
    public const string JwtSigningKey = "integration-tests-signing-key-not-a-real-secret-0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseSetting("Jwt:SigningKey", JwtSigningKey);

    builder.ConfigureServices(services =>
    {
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<AppDbContext>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString()));
    });
}

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
