using HashForge.Mining.Pools;
using HashForge.Mining.Statistics;

namespace HashForge.Web.Services;

public sealed class PoolOverview(IPoolRegistry registry, IStatisticsReader statistics) : IPoolOverview
{
    public IReadOnlyList<PoolSnapshot> GetPools() => [.. registry.Pools.Select(statistics.GetPool)];

    public PoolSnapshot? FindPool(string name) => registry.Find(name) is { } pool ? statistics.GetPool(pool) : null;

    public ShareTotals GetTotals()
    {
        var pools = GetPools();
        return new ShareTotals(pools.Sum(p => p.Stats.Accepted), pools.Sum(p => p.Stats.Rejected));
    }

    public BestShare? GetBestShare() =>
        GetPools().Select(p => p.Stats.BestShare).OfType<BestShare>().MaxBy(b => b.Difficulty);
}
