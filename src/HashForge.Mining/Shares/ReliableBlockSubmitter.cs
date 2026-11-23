using HashForge.Bitcoin.Rpc;
using HashForge.Mining.Jobs;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Shares;

/// <summary>Submits a solved block to the node, retrying on failure and logging the block if all attempts fail.</summary>
public sealed partial class ReliableBlockSubmitter(
    IBlockSubmitter submitter,
    TimeProvider timeProvider,
    ILogger<ReliableBlockSubmitter> logger) : IBlockCandidateSubmitter
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Submitting block {Height} ({Hash}) failed on attempt {Attempt}; retrying")]
    private partial void LogSubmitRetry(Exception exception, int height, string hash, int attempt);

    [LoggerMessage(Level = LogLevel.Critical, Message = "GAVE UP submitting block {Height} ({Hash}). Submit it manually with submitblock: {BlockHex}")]
    private partial void LogSubmitGaveUp(Exception exception, int height, string hash, string blockHex);
}