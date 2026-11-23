using HashForge.Mining.Jobs;
using HashForge.Stratum.Protocol;

namespace HashForge.Stratum.Sessions;

public sealed class WorkSender(IJobSource jobSource, TimeProvider timeProvider) : IWorkSender
{
    public Task SendDifficultyAsync(StratumSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Channel.SendAsync(JobNotification.SetDifficulty(session.VarDiff.Difficulty), cancellationToken);
    }

    public async Task SendJobAsync(StratumSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Workers.MinerAddress is not { } address)
        {
            return;
        }

        // Checked on every job, not only after a share, so a miner too slow to submit anything still gets
        // its difficulty lowered.
        if (session.VarDiff.TryRetarget(timeProvider.GetUtcNow()) is not null)
        {
            await SendDifficultyAsync(session, cancellationToken).ConfigureAwait(false);
        }

        var job = await jobSource.GetCurrentJobAsync(session.Pool, address, cancellationToken).ConfigureAwait(false);
        if (job is not null)
        {
            await session.Jobs.SendAsync(job, cancellationToken).ConfigureAwait(false);
        }
    }
}
