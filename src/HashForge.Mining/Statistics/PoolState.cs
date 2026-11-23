namespace HashForge.Mining.Statistics;

/// <summary>A pool's saved statistics, with those of each of its miners.</summary>
public sealed record PoolState(AccumulatorState Stats, Dictionary<string, MinerState> Miners);