using HashForge.Mining.Hosting;
using HashForge.Mining.Pools;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.History;

/// <summary>Background job that records each pool's hashrate at a slow, fixed interval.</summary>
public sealed class PoolHistorySampler(
    IPoolRegistry registry,
    IStatisticsReader statistics,
    IPoolHistoryRepository repository,
    TimeProvider timeProvider,
    IOptions<MiningOptions> options) : IPeriodicJob
{
    public string Name => "pool-history";

    public TimeSpan Interval { get; } = TimeSpan.FromMinutes(options.Value.HistorySampleMinutes);

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var samples = registry.Pools
            .Select(pool => statistics.GetPool(pool))
            .Select(pool => new PoolHistorySample(pool.Name, now, pool.Stats.Hashrate, pool.MinerCount, pool.WorkerCount))
            .ToList();

        return samples.Count == 0 ? Task.CompletedTask : repository.AddAsync(samples, cancellationToken);
    }
}
