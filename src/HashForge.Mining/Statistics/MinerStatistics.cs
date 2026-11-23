using System.Collections.Concurrent;

namespace HashForge.Mining.Statistics;

/// <summary>Live statistics for one miner (a payout address) and each of its workers.</summary>
public sealed class MinerStatistics(Func<StatsAccumulator> newAccumulator)
{
    public StatsAccumulator Stats { get; } = newAccumulator();

    public ConcurrentDictionary<string, StatsAccumulator> Workers { get; } = new(StringComparer.Ordinal);

    public StatsAccumulator WorkerFor(string name) => Workers.GetOrAdd(name, _ => newAccumulator());

    public void ResetRound()
    {
        Stats.ResetRound();
        foreach (var worker in Workers.Values)
        {
            worker.ResetRound();
        }
    }

    public MinerState Export() => new(Stats.Export(), Workers.ToDictionary(w => w.Key, w => w.Value.Export()));

    public void Import(MinerState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Stats.Import(state.Stats);
        foreach (var (workerName, workerState) in state.Workers)
        {
            WorkerFor(workerName).Import(workerState);
        }
    }
}