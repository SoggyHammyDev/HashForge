using HashForge.Domain;

namespace HashForge.Mining.Shares;

/// <summary>A block the pool found, with the payout split that was written into its coinbase.</summary>
public sealed record FoundBlock(
    string PoolName,
    PayoutScheme Scheme,
    int Height,
    string Hash,
    WorkerIdentity Worker,
    long RewardSatoshis,
    IReadOnlyList<PayoutEntry> Payouts,
    double EffortPercent,
    DateTimeOffset FoundAt,
    bool Accepted,
    string? RejectReason)
{
    /// <summary>How many miners the block paid, not counting the pool's fee output.</summary>
    public int MinersPaid { get; init; }

    /// <summary>
    /// Whether the payout at this position is the pool's fee rather than a miner's earnings. The fee, when
    /// there is one, is always the last output and is the only one not counted in <see cref="MinersPaid"/>.
    /// </summary>
    public bool IsPoolFee(int payoutIndex) => Payouts.Count == MinersPaid + 1 && payoutIndex == Payouts.Count - 1;
}
