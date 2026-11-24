using HashForge.Domain;

namespace HashForge.Mining.Shares;

/// <summary>A share exactly as a miner submitted it, plus what the pool knows about that miner's session.</summary>
/// <param name="VersionMask">The version-rolling mask negotiated with the miner (zero when not negotiated).</param>
/// <param name="PreviousDifficulty">The difficulty in force before the latest retarget, honoured for in-flight work.</param>
public sealed record ShareSubmission(
    string PoolName,
    WorkerIdentity Worker,
    string JobId,
    byte[] Extranonce1,
    string Extranonce2Hex,
    string TimeHex,
    string NonceHex,
    string? VersionBitsHex,
    uint VersionMask,
    double Difficulty,
    double? PreviousDifficulty)
{
    /// <summary>
    /// False when the job was never sent on the miner's current connection, such as work carried over a
    /// reconnect. That work was built with another extranonce, so it can only ever be stale.
    /// </summary>
    public bool JobWasSent { get; init; } = true;
}
