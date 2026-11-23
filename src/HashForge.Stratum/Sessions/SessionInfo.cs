using System.Net;

namespace HashForge.Stratum.Sessions;

/// <summary>A read-only description of a connected miner, for display.</summary>
public sealed record SessionInfo(
    string Id,
    string PoolName,
    IPAddress RemoteAddress,
    string? MinerAddress,
    IReadOnlyList<string> Workers,
    double Difficulty,
    long Accepted,
    long Rejected,
    DateTimeOffset ConnectedAt);
