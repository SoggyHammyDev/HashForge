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
            timeProvider.GetUtcNow()));
    }
}
