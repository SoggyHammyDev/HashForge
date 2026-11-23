using HashForge.Domain;

namespace HashForge.Mining.Statistics;

/// <summary>Read access to live statistics, for the UI and for anything else that only needs to look.</summary>
public interface IStatisticsReader
{
    PoolSnapshot GetPool(PoolDefinition pool);

    IReadOnlyList<MinerSnapshot> GetMiners(string poolName);

    MinerSnapshot? GetMiner(string poolName, string address);

    /// <summary>Effort for the round a block would close: the miner's own on a solo pool, otherwise the pool's.</summary>
    double GetRoundEffortPercent(PoolDefinition pool, string minerAddress);
}
