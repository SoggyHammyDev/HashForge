using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Jobs;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Shares;

/// <summary>Deals with the aftermath of a solved block: the archive, the announcements and fresh work.</summary>
public sealed partial class FoundBlockRecorder(
    IBlockRepository repository,
    IStatisticsReader statistics,
    IEnumerable<IBlockObserver> blockObservers,
    ITemplateRefresher templateRefresher,
    ILogger<FoundBlockRecorder> logger) : IFoundBlockRecorder
{
    public async Task RecordAsync(
        MiningJob job,
        BlockCandidate block,
        ShareResult share,
        BlockSubmissionResult nodeVerdict,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(share);
        ArgumentNullException.ThrowIfNull(nodeVerdict);

        var found = Describe(job, block, share, nodeVerdict);
        if (found.Accepted)
        {
            LogBlockFound(found.Height, found.Hash, found.PoolName, found.Worker.FullName);
        }
        else
        {
            LogBlockRejected(found.Height, found.Hash, found.RejectReason);
        }

        // Each step is independent: a failed archive must not stop the round being closed, and so on.
        await RunStepAsync("archive", () => repository.AddAsync(found, cancellationToken)).ConfigureAwait(false);
        if (!found.Accepted)
        {
            return;
        }

        foreach (var observer in blockObservers)
        {
            await RunStepAsync(observer.GetType().Name, () => observer.OnBlockFoundAsync(found, cancellationToken)).ConfigureAwait(false);
        }

        await RunStepAsync("template refresh", () => templateRefresher.RefreshAsync(cancellationToken)).ConfigureAwait(false);
    }

    private FoundBlock Describe(MiningJob job, BlockCandidate block, ShareResult share, BlockSubmissionResult nodeVerdict) => new(
        job.Pool.Name,
        job.Pool.Scheme,
        job.Template.Height,
        block.Hash,
        share.Worker,
        job.Template.CoinbaseValue,
        job.Payouts,
        statistics.GetRoundEffortPercent(job.Pool, share.Worker.Address),
        share.Timestamp,
        nodeVerdict.Accepted,
        nodeVerdict.RejectReason)
    {
        MinersPaid = job.MinersPaid,
    };

    private async Task RunStepAsync(string step, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The block is already on the chain; no follow-up failure may prevent the remaining steps.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogStepFailed(exception, step);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "BLOCK FOUND at height {Height} ({Hash}) on pool {Pool} by {Worker}")]
    private partial void LogBlockFound(int height, string hash, string pool, string worker);

    [LoggerMessage(Level = LogLevel.Error, Message = "The node rejected block {Height} ({Hash}): {Reason}")]
    private partial void LogBlockRejected(int height, string hash, string? reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "After a block was found, the '{Step}' step failed")]
    private partial void LogStepFailed(Exception exception, string step);
}