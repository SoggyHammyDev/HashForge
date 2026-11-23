using System.Collections.Concurrent;
using System.Text.Json;
using HashForge.Domain;
using HashForge.Mining.Network;
using HashForge.Mining.Shares;
using HashForge.Mining.State;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Statistics;

/// <summary>Keeps live statistics for every pool, miner and worker, entirely in memory.</summary>
public sealed class StatisticsService(
    INetworkStatsStore networkStats,
    TimeProvider timeProvider,
    IEnumerable<IBestShareListener> bestShareListeners,
    IOptions<MiningOptions> options) : IStatisticsReader, IShareObserver, IBlockObserver, IStatePersistable
{
    private readonly TimeSpan _hashrateWindow = TimeSpan.FromMinutes(options.Value.HashrateWindowMinutes);
    private readonly ConcurrentDictionary<string, PoolEntry> _pools = new(StringComparer.OrdinalIgnoreCase);

    public string StateKey => "statistics";

    private double NetworkDifficulty => networkStats.Current?.Difficulty ?? 0;

    public void OnShare(ShareResult share)
    {
        ArgumentNullException.ThrowIfNull(share);

        var pool = PoolFor(share.PoolName);
        var miner = pool.MinerFor(share.Worker.Address, NewAccumulator);
        var isPoolBest = pool.Stats.Record(share);
        miner.Stats.Record(share);
        miner.WorkerFor(share.Worker.WorkerName, NewAccumulator).Record(share);
        if (isPoolBest)
        {
            foreach (var listener in bestShareListeners)
            {
                listener.OnPoolBestShare(share);
            }
        }
    }

    public Task OnBlockFoundAsync(FoundBlock block, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (_pools.TryGetValue(block.PoolName, out var pool))
        {
            pool.Stats.ResetRound();
            // On a solo pool each miner works their own round, so only the finder's round ends.
            var finishedMiners = block.Scheme.HasPerMinerRounds()
                ? pool.Miners.Where(m => m.Key == block.Worker.Address)
                : pool.Miners;
            foreach (var (_, miner) in finishedMiners)
            {
                miner.ResetRound();
            }
        }

        return Task.CompletedTask;
    }

    public PoolSnapshot GetPool(PoolDefinition pool)
    {
        ArgumentNullException.ThrowIfNull(pool);

        // "Miners" and "workers" mean those seen recently, not everyone who has ever connected.
        var entry = PoolFor(pool.Name);
        var activeSince = timeProvider.GetUtcNow() - _hashrateWindow;
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
        _pools.TryGetValue(poolName, out var pool)
            ? [.. pool.Miners.Select(m => ToSnapshot(m.Key, m.Value)).OrderByDescending(m => m.Stats.Hashrate)]
            : [];

    public MinerSnapshot? GetMiner(string poolName, string address) =>
        _pools.TryGetValue(poolName, out var pool) && pool.Miners.TryGetValue(address, out var miner)
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

    public string ExportState()
    {
        var state = _pools.ToDictionary(p => p.Key, p => p.Value.Export());
        return JsonSerializer.Serialize(state);
    }

    public void ImportState(string state)
    {
        var restored = JsonSerializer.Deserialize<Dictionary<string, PoolState>>(state) ?? [];
        foreach (var (poolName, poolState) in restored)
        {
            PoolFor(poolName).Import(poolState, NewAccumulator);
        }
    }

    private StatsAccumulator NewAccumulator() => new(_hashrateWindow);

    private PoolEntry PoolFor(string poolName) => _pools.GetOrAdd(poolName, _ => new PoolEntry(NewAccumulator()));

    private StatsSnapshot Snapshot(StatsAccumulator accumulator) =>
        accumulator.Snapshot(timeProvider.GetUtcNow(), NetworkDifficulty);

    private MinerSnapshot ToSnapshot(string address, MinerEntry miner) => new(
        address,
        Snapshot(miner.Stats),
        [.. miner.Workers.Select(w => new WorkerSnapshot(w.Key, Snapshot(w.Value))).OrderBy(w => w.Name, StringComparer.Ordinal)]);

    private sealed record MinerState(AccumulatorState Stats, Dictionary<string, AccumulatorState> Workers);

    private sealed record PoolState(AccumulatorState Stats, Dictionary<string, MinerState> Miners);

    private sealed class MinerEntry(StatsAccumulator stats)
    {
        public StatsAccumulator Stats { get; } = stats;

        public ConcurrentDictionary<string, StatsAccumulator> Workers { get; } = new(StringComparer.Ordinal);

        public StatsAccumulator WorkerFor(string name, Func<StatsAccumulator> create) => Workers.GetOrAdd(name, _ => create());

        public void ResetRound()
        {
            Stats.ResetRound();
            foreach (var worker in Workers.Values)
            {
                worker.ResetRound();
            }
        }

        public MinerState Export() => new(Stats.Export(), Workers.ToDictionary(w => w.Key, w => w.Value.Export()));
    }

    private sealed class PoolEntry(StatsAccumulator stats)
    {
        public StatsAccumulator Stats { get; } = stats;

        public ConcurrentDictionary<string, MinerEntry> Miners { get; } = new(StringComparer.Ordinal);

        public MinerEntry MinerFor(string address, Func<StatsAccumulator> create) =>
            Miners.GetOrAdd(address, _ => new MinerEntry(create()));

        public PoolState Export() => new(Stats.Export(), Miners.ToDictionary(m => m.Key, m => m.Value.Export()));

        public void Import(PoolState state, Func<StatsAccumulator> create)
        {
            Stats.Import(state.Stats);
            foreach (var (address, minerState) in state.Miners)
            {
                var miner = MinerFor(address, create);
                miner.Stats.Import(minerState.Stats);
                foreach (var (workerName, workerState) in minerState.Workers)
                {
                    miner.WorkerFor(workerName, create).Import(workerState);
                }
            }
        }
    }
}
