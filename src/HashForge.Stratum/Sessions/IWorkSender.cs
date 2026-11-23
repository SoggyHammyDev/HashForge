namespace HashForge.Stratum.Sessions;

/// <summary>Sends miners their difficulty and their current job.</summary>
public interface IWorkSender
{
    Task SendDifficultyAsync(StratumSession session, CancellationToken cancellationToken);

    Task SendJobAsync(StratumSession session, bool cleanJobs, CancellationToken cancellationToken);
}
