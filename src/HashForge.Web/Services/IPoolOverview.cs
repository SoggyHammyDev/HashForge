using HashForge.Mining.Statistics;

namespace HashForge.Web.Services;

/// <summary>Statistics summed or compared across every running pool.</summary>
public interface IPoolOverview
{
    IReadOnlyList<PoolSnapshot> GetPools();

    PoolSnapshot? FindPool(string name);

    ShareTotals GetTotals();

    /// <summary>The best share submitted to any pool, with the worker that found it.</summary>
    BestShare? GetBestShare();
}
