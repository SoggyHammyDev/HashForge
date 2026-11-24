using HashForge.Mining.Hosting;
using HashForge.Mining.Pools;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.History;

/// <summary>Background job that records the hashrate of every miner currently hashing, at a slow, fixed interval.</summary>
public sealed class MinerHistorySampler(
    IPoolRegistry registry,
    IStatisticsReader statistics,
    MinerHistoryStore store,
    TimeProvider timeProvider,
    IOptions<MiningOptions> options) : IPeriodicJob
{
    public string Name => "miner-history";

    public TimeSpan Interval { get; } = TimeSpan.FromMinutes(options.Value.HistorySampleMinutes);

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var pool in registry.Pools)
        {
            foreach (var miner in statistics.GetMiners(pool.Name).Where(m => m.Stats.Hashrate > 0))
            {
                store.Add(pool.Name, miner.Address, new MinerHistorySample(
                    now.ToUnixTimeSeconds(),
                    Math.Round(miner.Stats.Hashrate),
                    miner.Workers.Count(w => w.Stats.Hashrate > 0)));
            }
        }

        store.Prune(now);
        return Task.CompletedTask;
    }
}