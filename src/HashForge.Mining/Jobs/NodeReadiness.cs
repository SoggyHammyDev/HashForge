using HashForge.Mining.Network;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Jobs;

public sealed partial class NodeReadiness(INetworkStatsStore networkStats, ILogger<NodeReadiness> logger) : INodeReadiness
{
    private int _paused;

    public bool IsReady()
    {
        // Test networks hand out templates even mid-sync, so the pool checks the node's progress itself.
        if (networkStats.Current is not { } node)
        {
            ReportNotReady("waiting for the node to report its status");
            return false;
        }

        if (!node.IsReadyForMining)
        {
            ReportNotReady("the node is still synchronising the blockchain");
            return false;
        }

        return true;
    }

    /// <summary>Logs the reason once, not on every poll.</summary>
    public void ReportNotReady(string reason)
    {
        if (Interlocked.Exchange(ref _paused, 1) == 0)
        {
            LogPaused(reason);
        }
    }

    public void ReportReady()
    {
        if (Interlocked.Exchange(ref _paused, 0) == 1)
        {
            LogResumed();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mining is paused until the Bitcoin node is ready: {Reason}")]
    private partial void LogPaused(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "The Bitcoin node is ready; mining resumed")]
    private partial void LogResumed();
}