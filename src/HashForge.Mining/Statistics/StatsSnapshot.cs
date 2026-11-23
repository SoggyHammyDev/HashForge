using HashForge.Domain;

namespace HashForge.Mining.Statistics;

/// <summary>Read-only statistics for a worker, a miner or a whole pool.</summary>
public sealed record StatsSnapshot(
    long Accepted,
    long Rejected,
    IReadOnlyDictionary<ShareOutcome, long> RejectedByReason,
    double RoundDifficulty,
    double EffortPercent,
    double Hashrate,
    BestShare? BestShare,
    DateTimeOffset? LastShareAt);
