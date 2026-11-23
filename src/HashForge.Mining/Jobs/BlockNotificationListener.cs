using HashForge.Bitcoin.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Jobs;

/// <summary>
/// Reacts to pushed block announcements by fetching new work immediately, so miners stop working on a
/// block that has already been solved as soon as the node knows about it. Polling continues alongside as
/// a safety net.
/// </summary>
public sealed partial class BlockNotificationListener(
    IBlockNotificationSource source,
    ITemplateRefresher refresher,
    INodeReadiness readiness,
    TimeProvider timeProvider,
    ILogger<BlockNotificationListener> logger) : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!source.IsEnabled)
        {
            LogDisabled();
            return;
        }

        LogListening();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var blockHash in source.ListenAsync(stoppingToken).ConfigureAwait(false))
                {
                    await OnBlockAnnouncedAsync(blockHash, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Whatever broke the feed, the right response is to log it and listen again.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogFeedFailed(exception);
            }

            await Task.Delay(ReconnectDelay, timeProvider, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task OnBlockAnnouncedAsync(string blockHash, CancellationToken cancellationToken)
    {
        // During the initial download blocks arrive in a flood and none of them is worth mining on.
        if (!readiness.IsReady())
        {
            return;
        }

        LogBlockAnnounced(blockHash);
        try
        {
            await refresher.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // One failed refresh must not end the feed; the poller will catch the block.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogRefreshFailed(exception, blockHash);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening for new blocks pushed by the node over ZeroMQ")]
    private partial void LogListening();

    [LoggerMessage(Level = LogLevel.Warning, Message = "No ZeroMQ address is configured, so new blocks are detected by polling only. Set BitcoinNode:ZmqBlockUrl for instant detection")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Node announced block {BlockHash}")]
    private partial void LogBlockAnnounced(string blockHash);

    [LoggerMessage(Level = LogLevel.Error, Message = "The ZeroMQ block feed failed; reconnecting shortly. Polling continues meanwhile")]
    private partial void LogFeedFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fetching new work after block {BlockHash} failed; the poller will retry")]
    private partial void LogRefreshFailed(Exception exception, string blockHash);
}