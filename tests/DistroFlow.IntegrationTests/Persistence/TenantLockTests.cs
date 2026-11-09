using DistroFlow.Application.Interfaces.Persistence;
using DistroFlow.Application.Interfaces.Repositories;
using DistroFlow.Domain.Entities;
using DistroFlow.Infrastructure.Persistence;
using DistroFlow.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DistroFlow.IntegrationTests.Persistence;

/// <summary>Proves the tenant row lock really makes a second transaction wait.</summary>
public class TenantLockTests : IClassFixture<ApiIntegrationTestFactory>
{
    private readonly ApiIntegrationTestFactory _factory;

    public TenantLockTests(ApiIntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Second_transaction_cannot_lock_the_same_tenant_until_the_first_ends()
    {
        var tenantId = await CreateTenantAsync();

        await using var first = _factory.Services.CreateAsyncScope();
        await using var firstTx = await UnitOfWork(first).BeginTransactionAsync();
        await Tenants(first).LockForUpdateAsync(tenantId);

        // While the first holds the lock, the second gives up after a short timeout.
        var ex = await Assert.ThrowsAsync<PostgresException>(() => TryLockWithTimeoutAsync(tenantId));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, ex.SqlState);

        await firstTx.CommitAsync();

        // Once the first transaction ends, the lock is free again.
        await TryLockWithTimeoutAsync(tenantId);
    }

    [Fact]
    public async Task Locking_one_tenant_does_not_block_another()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();

        await using var first = _factory.Services.CreateAsyncScope();
        await using var firstTx = await UnitOfWork(first).BeginTransactionAsync();
        await Tenants(first).LockForUpdateAsync(tenantA);

        await TryLockWithTimeoutAsync(tenantB); // does not throw
    }

    [Fact]
    public async Task Locking_outside_a_transaction_is_rejected()
    {
        var tenantId = await CreateTenantAsync();
        await using var scope = _factory.Services.CreateAsyncScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Tenants(scope).LockForUpdateAsync(tenantId));
    }

    /// <summary>Tries to lock in a separate transaction, waiting at most 300 ms.</summary>
    private async Task TryLockWithTimeoutAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await using var tx = await UnitOfWork(scope).BeginTransactionAsync();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'");

        await Tenants(scope).LockForUpdateAsync(tenantId);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = new Tenant { Name = $"Distributor {Guid.NewGuid():N}" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static IUnitOfWork UnitOfWork(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    private static ITenantRepository Tenants(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ITenantRepository>();
}
