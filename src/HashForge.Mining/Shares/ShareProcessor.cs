using HashForge.Domain;
using HashForge.Mining.Jobs;
using Microsoft.Extensions.Logging;

namespace HashForge.Mining.Shares;

/// <summary>Validates each share, gets any solved block to the node first, then tells the observers.</summary>
public sealed partial class ShareProcessor(
    IJobSource jobSource,
    IShareValidator validator,
    IEnumerable<IShareObserver> shareObservers,
    IBlockCandidateHandler blockHandler,
    TimeProvider timeProvider,
    ILogger<ShareProcessor> logger) : IShareProcessor
{
    public async Task<ShareResult> ProcessAsync(ShareSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var job = jobSource.Find(submission.JobId);
        var validation = job is not null && string.Equals(job.Pool.Name, submission.PoolName, StringComparison.OrdinalIgnoreCase)
            ? validator.Validate(submission, job)
            : ShareValidation.Rejected(ShareOutcome.StaleJob);

        // Nothing is allowed to come between solving a block and handing it to the node.
        var nodeVerdict = job is not null && validation.Block is { } block
            ? await blockHandler.SubmitAsync(job, block).ConfigureAwait(false)
            : null;

        var result = new ShareResult(
            submission.PoolName,
            submission.Worker,
            validation.Outcome,
            validation.CreditedDifficulty,
            validation.ActualDifficulty,
            timeProvider.GetUtcNow());
        NotifyObservers(result);

        if (nodeVerdict is not null)
        {
            await blockHandler.RecordAsync(job!, validation.Block!, result, nodeVerdict, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private void NotifyObservers(ShareResult result)
    {
        foreach (var observer in shareObservers)
        {
            try
            {
                observer.OnShare(result);
            }
#pragma warning disable CA1031 // One observer failing must not stop the others, or the miner's reply.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogObserverFailed(exception, observer.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Share observer {Observer} failed")]
    private partial void LogObserverFailed(Exception exception, string observer);
}
