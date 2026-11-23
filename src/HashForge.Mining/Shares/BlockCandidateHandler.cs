using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.History;
using HashForge.Mining.Jobs;
using HashForge.Mining.Statistics;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Shares;

public sealed partial class BlockCandidateHandler(
    IBlockSubmitter submitter,
    IBlockRepository repository,
    IStatisticsReader statistics,
    IEnumerable<IBlockObserver> blockObservers,
    ITemplateRefresher templateRefresher,
    TimeProvider timeProvider,
    ILogger<BlockCandidateHandler> logger) : IBlockCandidateHandler
{
    public const int SubmitAttempts = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    public async Task<BlockSubmissionResult> SubmitAsync(MiningJob job, BlockCandidate block)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(block);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await submitter.SubmitBlockAsync(block.Hex, CancellationToken.None).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Whatever went wrong, the only sensible response is to try again and keep the block.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                if (attempt == SubmitAttempts)
                {
                    // The full block is logged so that it can still be submitted by hand.
                    LogSubmitGaveUp(exception, job.Template.Height, block.Hash, block.Hex);
                    return new BlockSubmissionResult($"could not reach the node: {exception.Message}");
                }

                LogSubmitRetry(exception, job.Template.Height, block.Hash, attempt);
                await Task.Delay(RetryDelay, timeProvider, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

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

        var found = new FoundBlock(
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Submitting block {Height} ({Hash}) failed on attempt {Attempt}; retrying")]
    private partial void LogSubmitRetry(Exception exception, int height, string hash, int attempt);

    [LoggerMessage(Level = LogLevel.Critical, Message = "GAVE UP submitting block {Height} ({Hash}). Submit it manually with submitblock: {BlockHex}")]
    private partial void LogSubmitGaveUp(Exception exception, int height, string hash, string blockHex);

    [LoggerMessage(Level = LogLevel.Error, Message = "After a block was found, the '{Step}' step failed")]
    private partial void LogStepFailed(Exception exception, string step);
}
