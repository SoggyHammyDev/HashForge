using HashForge.Domain;

namespace HashForge.Mining.Statistics;

/// <summary>The part of an accumulator worth keeping across restarts.</summary>
public sealed record AccumulatorState(
    long Accepted,
    Dictionary<ShareOutcome, long> Rejected,
    double RoundDifficulty,
    BestShare? BestShare,
    DateTimeOffset? LastShareAt);
