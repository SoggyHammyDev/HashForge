using System.Net;

namespace HashForge.Stratum.Sessions;

/// <summary>The miners that are connected right now.</summary>
public interface ISessionRegistry
{
    /// <summary>A copy of the current list. Use <see cref="Count"/> when only the number is needed.</summary>
    IReadOnlyList<StratumSession> Sessions { get; }

    int Count { get; }

    /// <summary>How many of the connected miners come from this IP address.</summary>
    int CountFrom(IPAddress remoteAddress);

    void Add(StratumSession session);

    void Remove(StratumSession session);
}
