using System.Text.Json;
using HashForge.Mining.State;

namespace HashForge.Mining.Statistics;

/// <summary>Saves and restores the live statistics so counters, effort and best shares survive a restart.</summary>
public sealed class StatisticsState(IStatisticsStore store) : IStatePersistable
{
    public string StateKey => "statistics";

    public string ExportState() =>
        JsonSerializer.Serialize(store.Pools.ToDictionary(p => p.Key, p => p.Value.Export()));

    public void ImportState(string state)
    {
        var restored = JsonSerializer.Deserialize<Dictionary<string, PoolState>>(state) ?? [];
        foreach (var (poolName, poolState) in restored)
        {
            store.PoolFor(poolName).Import(poolState);
        }
    }
}