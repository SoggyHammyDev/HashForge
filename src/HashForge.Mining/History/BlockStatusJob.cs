using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Hosting;
using HashForge.Mining.Network;
using HashForge.Mining.Shares;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HashForge.Mining.History;

/// <summary>
/// Background job that keeps the archive honest about each found block. A block the node accepted can still
/// lose to a competing block at the same height, so the node is asked whether each recent block is on its
/// best chain and how deep.
/// </summary>
public sealed partial class BlockStatusJob(
    IBlockRepository repository,
    IBlockHeaderSource headers,
    INetworkStatsStore networkStats,
    IOptions<MiningOptions> options,
    ILogger<BlockStatusJob> logger) : IPeriodicJob
{
    /// <summary>
    /// How far below the tip a block is still checked. It must exceed the maturity depth so every block is
    /// seen reaching it, and leaves room for an orphaned block to return in a deep reorganisation.
    /// </summary>
    public const int WatchDepth = 2 * FoundBlock.MaturityConfirmations;

    public string Name => "block-status";

    public TimeSpan Interval { get; } = TimeSpan.FromSeconds(options.Value.NetworkStatsRefreshSeconds);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (networkStats.Current is not { } network)
        {
            return;
        }

        var blocks = await repository.GetUnsettledAsync(network.Height - WatchDepth, cancellationToken).ConfigureAwait(false);
        foreach (var block in blocks)
        {
            // A block the node has never heard of is left as it is: that says nothing about the chain.
            if (await headers.FindBlockHeaderAsync(block.Hash, cancellationToken).ConfigureAwait(false) is not { } header)
            {
                continue;
            }

            var orphaned = !header.IsOnBestChain;
            var confirmations = orphaned ? 0 : header.Confirmations;
            if (orphaned == block.Orphaned && confirmations == block.Confirmations)
            {
                continue;
            }

            await repository.UpdateChainStateAsync(block.Hash, confirmations, orphaned, cancellationToken).ConfigureAwait(false);
            if (orphaned != block.Orphaned)
            {
                LogOrphanChange(block.Height, block.Hash, orphaned ? "has been orphaned" : "is back on the chain");
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Block {Height} ({Hash}) {Change}")]
    private partial void LogOrphanChange(int height, string hash, string change);
}
