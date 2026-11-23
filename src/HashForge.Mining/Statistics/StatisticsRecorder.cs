using HashForge.Domain;
using HashForge.Mining.Shares;

namespace HashForge.Mining.Statistics;

/// <summary>Updates the live statistics as shares arrive and blocks are found.</summary>
public sealed class StatisticsRecorder(IStatisticsStore store, IEnumerable<IBestShareListener> bestShareListeners)
    : IShareObserver, IBlockObserver
{
    public void OnShare(ShareResult share)
    {
        ArgumentNullException.ThrowIfNull(share);

        var pool = store.PoolFor(share.PoolName);
        var miner = pool.MinerFor(share.Worker.Address);
        var isPoolBest = pool.Stats.Record(share);
        miner.Stats.Record(share);
        miner.WorkerFor(share.Worker.WorkerName).Record(share);
        if (isPoolBest)
        {
            foreach (var listener in bestShareListeners)
            {
                listener.OnPoolBestShare(share);
            }
        }
    }

    public Task OnBlockFoundAsync(FoundBlock block, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);

        if (store.Find(block.PoolName) is { } pool)
        {
            pool.Stats.ResetRound();

            // On a solo pool each miner works their own round, so only the finder's round ends.
            var finishedMiners = block.Scheme.HasPerMinerRounds()
                ? pool.Miners.Where(m => m.Key == block.Worker.Address)
                : pool.Miners;
            foreach (var (_, miner) in finishedMiners)
            {
                miner.ResetRound();
            }
        }

        return Task.CompletedTask;
    }
}