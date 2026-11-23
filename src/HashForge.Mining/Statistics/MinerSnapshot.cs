namespace HashForge.Mining.Statistics;

public sealed record MinerSnapshot(string Address, StatsSnapshot Stats, IReadOnlyList<WorkerSnapshot> Workers);
