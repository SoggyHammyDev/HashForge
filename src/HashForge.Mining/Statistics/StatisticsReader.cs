using HashForge.Domain;
using HashForge.Mining.Network;

namespace HashForge.Mining.Statistics;

/// <summary>Turns the live statistics into read-only snapshots for the UI, the API and block records.</summary>
public sealed class StatisticsReader(IStatisticsStore store, INetworkStatsStore networkStats, TimeProvider timeProvider) : IStatisticsReader
{
    public PoolSnapshot GetPool(PoolDefinition pool)
    {
        ArgumentNullException.ThrowIfNull(pool);

        // "Miners" and "workers" mean those seen recently, not everyone who has ever connected.
        var entry = store.PoolFor(pool.Name);
        var activeSince = timeProvider.GetUtcNow() - store.HashrateWindow;
        var activeMiners = entry.Miners.Values.Where(m => m.Stats.IsActiveSince(activeSince)).ToList();
        return new PoolSnapshot(
            pool.Name,
            pool.Scheme,
            pool.Port,
            Snapshot(entry.Stats),
            activeMiners.Count,
            activeMiners.Sum(m => m.Workers.Values.Count(w => w.IsActiveSince(activeSince))));
    }

    public IReadOnlyList<MinerSnapshot> GetMiners(string poolName) =>
        store.Find(poolName) is { } pool
            ? [.. pool.Miners.Select(m => ToSnapshot(m.Key, m.Value)).OrderByDescending(m => m.Stats.Hashrate)]
            : [];

    public MinerSnapshot? GetMiner(string poolName, string address) =>
        store.Find(poolName) is { } pool && pool.Miners.TryGetValue(address, out var miner)
            ? ToSnapshot(address, miner)
            : null;

    public double GetRoundEffortPercent(PoolDefinition pool, string minerAddress)
    {
        ArgumentNullException.ThrowIfNull(pool);

        var stats = pool.Scheme.HasPerMinerRounds()
            ? GetMiner(pool.Name, minerAddress)?.Stats
            : GetPool(pool).Stats;
        return stats?.EffortPercent ?? 0;
    }

    private StatsSnapshot Snapshot(StatsAccumulator accumulator) =>
        accumulator.Snapshot(timeProvider.GetUtcNow(), networkStats.Current?.Difficulty ?? 0);

    private MinerSnapshot ToSnapshot(string address, MinerStatistics miner) => new(
        address,
        Snapshot(miner.Stats),
        [.. miner.Workers.Select(w => new WorkerSnapshot(w.Key, Snapshot(w.Value))).OrderBy(w => w.Name, StringComparer.Ordinal)]);
}