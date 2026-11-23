namespace HashForge.Mining.Statistics;

/// <summary>Where the live statistics are held in memory. Recording, reading and saving each go through this.</summary>
public interface IStatisticsStore
{
    /// <summary>How far back "recent" reaches for hashrate and for counting a miner as active.</summary>
    TimeSpan HashrateWindow { get; }

    IReadOnlyCollection<KeyValuePair<string, PoolStatistics>> Pools { get; }

    /// <summary>The pool's statistics, created on first use.</summary>
    PoolStatistics PoolFor(string poolName);

    /// <summary>The pool's statistics, or null when nothing has been recorded for it.</summary>
    PoolStatistics? Find(string poolName);
}