using HashForge.Stratum.Sessions;

namespace HashForge.Stratum.Server;

public interface ISessionRunner
{
    /// <summary>Serves one miner until it disconnects, misbehaves, goes idle or the pool stops.</summary>
    Task RunAsync(StratumSession session, CancellationToken cancellationToken);
}
