using System.Collections.Concurrent;

using System.Net;

namespace HashForge.Stratum.Sessions;

public sealed class SessionRegistry : ISessionRegistry
{
    private readonly ConcurrentDictionary<string, StratumSession> _sessions = new(StringComparer.Ordinal);

    public IReadOnlyList<StratumSession> Sessions => [.. _sessions.Values];

    public int Count => _sessions.Count;

    public int CountFrom(IPAddress remoteAddress)
    {
        var count = 0;
        foreach (var session in _sessions)
        {
            if (session.Value.RemoteAddress.Equals(remoteAddress))
            {
                count++;
            }
        }

        return count;
    }

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
