using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Hosting;
using HashForge.Mining.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.Jobs;

/// <summary>
/// Background job that watches the chain tip and publishes a new block template
/// whenever a block arrives or the current template has grown stale.
/// </summary>
public sealed partial class TemplateMonitor(
    IChainTipSource tipSource,
    IBlockTemplateSource templateSource,
    ITemplateSink sink,
    INetworkStatsStore networkStats,
    TimeProvider timeProvider,
    IOptions<MiningOptions> options,
    ILogger<TemplateMonitor> logger) : IPeriodicJob, ITemplateRefresher, IDisposable
{
    // The poller and a just-found block can both ask for a refresh; they take turns so templates stay in order.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <summary>Bitcoin Core's RPC_CLIENT_NOT_CONNECTED error code.</summary>
    private const int NodeNotConnected = -9;

    /// <summary>Bitcoin Core's RPC_CLIENT_IN_INITIAL_DOWNLOAD error code.</summary>
    private const int NodeInInitialDownload = -10;

    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(options.Value.TemplateRefreshSeconds);
    private bool _waitingForNode;
    private string? _lastTip;
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;

    public string Name => "template-monitor";

    public TimeSpan Interval { get; } = TimeSpan.FromSeconds(options.Value.ChainTipPollSeconds);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Test networks hand out templates even mid-sync, so the pool checks the node's progress itself.
        if (networkStats.Current is not { } node)
        {
            Pause("waiting for the node to report its status");
            return;
        }

        if (!node.IsReadyForMining)
        {
            Pause("the node is still synchronising the blockchain");
            return;
        }

        var tip = await tipSource.GetBestBlockHashAsync(cancellationToken).ConfigureAwait(false);
        var isStale = timeProvider.GetUtcNow() - _lastRefresh >= _refreshInterval;
        if (tip != _lastTip || isStale)
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _refreshGate.Dispose();

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        BlockTemplate template;
        try
        {
            template = await templateSource.GetBlockTemplateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BitcoinRpcException exception) when (exception.Code is NodeNotConnected or NodeInInitialDownload)
        {
            Pause(exception.Message);
            return;
        }

        _waitingForNode = false;
        if (template.PreviousBlockHash != _lastTip)
        {
            LogNewBlock(template.Height, template.PreviousBlockHash);
        }

        _lastTip = template.PreviousBlockHash;
        _lastRefresh = timeProvider.GetUtcNow();
        sink.Publish(template);
    }

    /// <summary>Records that no work can be handed out yet, logging the reason once rather than on every poll.</summary>
    private void Pause(string reason)
    {
        if (!_waitingForNode)
        {
            LogNodeNotReady(reason);
        }

        _waitingForNode = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mining is paused until the Bitcoin node is ready: {Reason}")]
    private partial void LogNodeNotReady(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Now mining block {Height} on top of {PreviousBlockHash}")]
    private partial void LogNewBlock(int height, string previousBlockHash);
}
