namespace HashForge.Mining.Statistics;

/// <summary>A miner's saved statistics, with those of each of its workers.</summary>
public sealed record MinerState(AccumulatorState Stats, Dictionary<string, AccumulatorState> Workers);