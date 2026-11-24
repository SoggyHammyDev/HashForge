using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Hosting;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Network;

/// <summary>Background job that keeps the network stats fresh.</summary>
public sealed class NetworkStatsJob(
    IChainInfoSource chainInfoSource,
    INetworkStatsStore store,
    TimeProvider timeProvider,
    IOptions<MiningOptions> options) : IPeriodicJob
{
    public string Name => "network-stats";

    public TimeSpan Interval { get; } = TimeSpan.FromSeconds(options.Value.NetworkStatsRefreshSeconds);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var info = await chainInfoSource.GetChainInfoAsync(cancellationToken).ConfigureAwait(false);
        var hashrate = await chainInfoSource.GetNetworkHashrateAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        store.Update(new NetworkStats(
            info.Chain,
            info.Blocks,
            info.Headers,
            info.Difficulty,
            hashrate,
            info.BestBlockHash,
            info.InitialBlockDownload,
            info.VerificationProgress,
            info.Pruned,
            info.SizeOnDisk,
            now)
        {
            LastBlockAt = LastBlockAt(info, now),
        });
    }

    /// <summary>
    /// A block's own timestamp is chosen by its miner and may be up to two hours ahead of the real time,
    /// which testnet miners do routinely. A block that arrives while the pool is watching is therefore dated
    /// by when it was first seen; the block's timestamp is only used for the tip found at start-up, and
    /// never beyond the present.
    /// </summary>
    private DateTimeOffset? LastBlockAt(ChainInfo info, DateTimeOffset now)
    {
        if (store.Current is { } previous)
        {
            return previous.BestBlockHash == info.BestBlockHash ? previous.LastBlockAt : now;
        }

        if (info.Time <= 0)
        {
            return null;
        }

        var stamped = DateTimeOffset.FromUnixTimeSeconds(info.Time);
        return stamped < now ? stamped : now;
    }
}
