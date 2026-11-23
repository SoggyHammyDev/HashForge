using System.Collections.Concurrent;

namespace HashForge.Mining.Statistics;

/// <summary>Live statistics for one pool and each of its miners.</summary>
public sealed class PoolStatistics(Func<StatsAccumulator> newAccumulator)
{
    public StatsAccumulator Stats { get; } = newAccumulator();

    public ConcurrentDictionary<string, MinerStatistics> Miners { get; } = new(StringComparer.Ordinal);

    public MinerStatistics MinerFor(string address) => Miners.GetOrAdd(address, _ => new MinerStatistics(newAccumulator));

    public PoolState Export() => new(Stats.Export(), Miners.ToDictionary(m => m.Key, m => m.Value.Export()));

    public void Import(PoolState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Stats.Import(state.Stats);
        foreach (var (address, minerState) in state.Miners)
        {
            MinerFor(address).Import(minerState);
        }
    }
}