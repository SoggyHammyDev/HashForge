using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Statistics;

public sealed class StatisticsStore(IOptions<MiningOptions> options) : IStatisticsStore
{
    private readonly ConcurrentDictionary<string, PoolStatistics> _pools = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan HashrateWindow { get; } = TimeSpan.FromMinutes(options.Value.HashrateWindowMinutes);

    public IReadOnlyCollection<KeyValuePair<string, PoolStatistics>> Pools => [.. _pools];

    public PoolStatistics PoolFor(string poolName) => _pools.GetOrAdd(poolName, _ => new PoolStatistics(NewAccumulator));

    public PoolStatistics? Find(string poolName) => _pools.GetValueOrDefault(poolName);

    private StatsAccumulator NewAccumulator() => new(HashrateWindow);
}