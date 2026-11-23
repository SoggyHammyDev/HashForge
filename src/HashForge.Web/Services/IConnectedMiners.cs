using HashForge.Stratum.Sessions;

namespace HashForge.Web.Services;

/// <summary>Who is connected to the Stratum ports right now.</summary>
public interface IConnectedMiners
{
    IReadOnlyList<SessionInfo> GetSessions();

    /// <summary>Each distinct IP address with its number of connections.</summary>
    IReadOnlyList<ConnectedAddress> GetAddresses();
}
