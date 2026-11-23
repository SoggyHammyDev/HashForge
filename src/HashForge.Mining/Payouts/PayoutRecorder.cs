using HashForge.Domain;
using HashForge.Mining.Network;
using HashForge.Mining.Pools;
using HashForge.Mining.Shares;

namespace HashForge.Mining.Payouts;

/// <summary>Feeds accepted shares and found blocks into each pool's payout scheme.</summary>
public sealed class PayoutRecorder(IPoolRegistry pools, IPayoutSchemeRegistry schemes, INetworkStatsStore networkStats)
    : IShareObserver, IBlockObserver
{
    public void OnShare(ShareResult share)
    {
        ArgumentNullException.ThrowIfNull(share);
        if (share.IsAccepted && pools.Find(share.PoolName) is { } pool)
        {
            schemes.SchemeFor(pool).RecordShare(share.Worker.Address, share.CreditedDifficulty, networkStats.Current?.Difficulty ?? 0);
        }
    }

    public Task OnBlockFoundAsync(FoundBlock block, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (pools.Find(block.PoolName) is { } pool)
        {
            schemes.SchemeFor(pool).OnBlockFound();
        }

        return Task.CompletedTask;
    }
}