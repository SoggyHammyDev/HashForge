namespace HashForge.Mining.History;

public interface IPoolHistoryRepository
{
    Task AddAsync(IReadOnlyList<PoolHistorySample> samples, CancellationToken cancellationToken);

    Task<IReadOnlyList<PoolHistorySample>> GetSinceAsync(string poolName, DateTimeOffset since, CancellationToken cancellationToken);
}
