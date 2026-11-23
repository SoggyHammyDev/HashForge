using System.Collections.Concurrent;

namespace HashForge.Stratum.Sessions;

public sealed class SessionRegistry : ISessionRegistry
{
    private readonly ConcurrentDictionary<string, StratumSession> _sessions = new(StringComparer.Ordinal);

    public IReadOnlyList<StratumSession> Sessions => [.. _sessions.Values];

    public void Add(StratumSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Id] = session;
    }

    public void Remove(StratumSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions.TryRemove(session.Id, out _);
    }
}
