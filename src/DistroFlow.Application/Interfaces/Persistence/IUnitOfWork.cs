namespace DistroFlow.Application.Interfaces.Persistence;

/// <summary>
/// Saves pending changes and groups several saves into one database transaction.
/// All repositories in a request share the same DbContext, so one transaction covers them all.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a transaction. Call <see cref="ITransaction.CommitAsync"/> to keep the changes;
    /// disposing without committing rolls everything back.
    /// </summary>
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
