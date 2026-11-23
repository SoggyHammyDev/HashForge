using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Hosting;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Jobs;

/// <summary>
/// Background job that asks the node for its chain tip at a short interval. It catches a new block even when
/// no push notification arrives, and refreshes work between blocks so it picks up new transactions.
/// </summary>
public sealed class ChainTipPoller(
    IChainTipSource tipSource,
    ITemplateRefresher refresher,
    INodeReadiness readiness,
    TimeProvider timeProvider,
    IOptions<MiningOptions> options) : IPeriodicJob
{
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(options.Value.TemplateRefreshSeconds);

    public string Name => "chain-tip-poller";

    public TimeSpan Interval { get; } = TimeSpan.FromSeconds(options.Value.ChainTipPollSeconds);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!readiness.IsReady())
        {
            return;
        }

        var tip = await tipSource.GetBestBlockHashAsync(cancellationToken).ConfigureAwait(false);
        var status = refresher.Status;
        var isStale = timeProvider.GetUtcNow() - status.RefreshedAt >= _refreshInterval;
        if (tip != status.Tip || isStale)
        {
            await refresher.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}