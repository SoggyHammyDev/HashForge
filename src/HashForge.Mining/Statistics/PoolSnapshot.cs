using HashForge.Domain;

namespace HashForge.Mining.Statistics;

public sealed record PoolSnapshot(string Name, PayoutScheme Scheme, int Port, StatsSnapshot Stats, int MinerCount, int WorkerCount);
