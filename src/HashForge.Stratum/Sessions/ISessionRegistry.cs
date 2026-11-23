namespace HashForge.Stratum.Sessions;

/// <summary>The miners that are connected right now.</summary>
public interface ISessionRegistry
{
    IReadOnlyList<StratumSession> Sessions { get; }

    void Add(StratumSession session);

    void Remove(StratumSession session);
}
