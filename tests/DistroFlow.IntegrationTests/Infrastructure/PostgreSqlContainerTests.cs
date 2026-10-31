using Npgsql;
using Testcontainers.PostgreSql;

namespace DistroFlow.IntegrationTests.Infrastructure;

public class PostgreSqlContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =  
     new PostgreSqlBuilder("postgres:18")
        .WithDatabase("distroflow_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();
   

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    [Fact]
    public async Task Database_ShouldExecuteQuery_Successfully()
    {
        // Arrange
        await using var connection =
            new NpgsqlConnection(_postgres.GetConnectionString());

        await connection.OpenAsync();

        // Act
        await using var command =
            new NpgsqlCommand("SELECT 1", connection);

        var result = await command.ExecuteScalarAsync();

        // Assert
        Assert.Equal(1, result);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}