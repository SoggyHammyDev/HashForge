using HashForge.Mining.Jobs;
using HashForge.Stratum.Protocol;

namespace HashForge.Stratum.Sessions;

/// <summary>
/// Sends jobs to one miner in strict order. Several senders can race (a new block, a routine refresh,
/// a difficulty change), so the decision to send is made while holding the wire: a job built from an older
/// template than the last one sent is dropped, and the miner can never be moved backwards.
/// </summary>
public sealed class JobDelivery(MinerChannel channel)
{
    private long _lastSequence = -1;
    private string? _lastPreviousBlockHash;

    public Task SendAsync(MiningJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        return channel.SendAsync(() => Compose(job), cancellationToken);
    }

    /// <summary>Runs under the channel's write lock, so no other job can be sent between the check and the write.</summary>
    private string? Compose(MiningJob job)
    {
        if (job.TemplateSequence < _lastSequence)
        {
            return null;
        }

        // The miner is told to drop its current work exactly when the chain tip it is building on has changed.
        var cleanJobs = job.Template.PreviousBlockHash != _lastPreviousBlockHash;
        _lastSequence = job.TemplateSequence;
        _lastPreviousBlockHash = job.Template.PreviousBlockHash;
        return JobNotification.Notify(job, cleanJobs);
    }
}