using HashForge.Stratum.Sessions;

namespace HashForge.Web.Services;

public sealed class ConnectedMiners(ISessionRegistry sessions) : IConnectedMiners
{
    public IReadOnlyList<SessionInfo> GetSessions() =>
        [.. sessions.Sessions.Select(s => s.ToInfo()).OrderBy(s => s.PoolName, StringComparer.Ordinal).ThenBy(s => s.ConnectedAt)];

    public IReadOnlyList<ConnectedAddress> GetAddresses() =>
    [
        .. sessions.Sessions
            .GroupBy(s => s.RemoteAddress)
            .Select(g => new ConnectedAddress(g.Key, g.Count()))
            .OrderByDescending(a => a.Connections)
            .ThenBy(a => a.Address.ToString(), StringComparer.Ordinal),
    ];
}
