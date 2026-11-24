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
    /// <summary>How deep a block must be before its reward can be spent.</summary>
    public const int MaturityConfirmations = 100;

    /// <summary>How many blocks deep it is on the chain, as last reported by the node. Zero until first checked.</summary>
    public int Confirmations { get; init; }

    /// <summary>True when the node accepted the block but the chain has since gone another way.</summary>
    public bool Orphaned { get; init; }

    public BlockStatus Status => this switch
    {
        { Accepted: false } => IsStaleReason(RejectReason) ? BlockStatus.Stale : BlockStatus.Rejected,
        { Orphaned: true } => BlockStatus.Orphaned,
        { Confirmations: >= MaturityConfirmations } => BlockStatus.Confirmed,
        _ => BlockStatus.Accepted,
    };

    /// <summary>How many miners the block paid, not counting the pool's fee output.</summary>
    public int MinersPaid { get; init; }

    /// <summary>
    /// Whether the payout at this position is the pool's fee rather than a miner's earnings. The fee, when
    /// there is one, is always the last output and is the only one not counted in <see cref="MinersPaid"/>.
    /// </summary>
    public bool IsPoolFee(int payoutIndex) => Payouts.Count == MinersPaid + 1 && payoutIndex == Payouts.Count - 1;

    // Bitcoin Core answers "inconclusive" for a valid block that does not extend its best chain, and uses
    // "stale" in the reasons for a block built on an old tip.
    private static bool IsStaleReason(string? reason) =>
        reason is not null
        && (reason.Contains("inconclusive", StringComparison.OrdinalIgnoreCase) || reason.Contains("stale", StringComparison.OrdinalIgnoreCase));
}
