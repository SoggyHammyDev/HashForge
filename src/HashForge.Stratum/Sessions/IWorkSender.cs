namespace HashForge.Stratum.Sessions;

/// <summary>Sends miners their difficulty and their current job.</summary>
public interface IWorkSender
{
    Task SendDifficultyAsync(StratumSession session, CancellationToken cancellationToken);

    /// <summary>Sends the miner its current job. Jobs are delivered in order; an out-of-date one is dropped.</summary>
    Task SendJobAsync(StratumSession session, CancellationToken cancellationToken);
}
