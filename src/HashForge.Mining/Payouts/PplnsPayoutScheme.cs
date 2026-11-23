namespace HashForge.Mining.Payouts;

/// <summary>
/// PPLNS: the reward is split by the most recent work, a sliding window worth a multiple of network
/// difficulty. History is kept in per-minute buckets so that old work can expire.
/// </summary>
public sealed class PplnsPayoutScheme(TimeProvider timeProvider, double windowMultiplier) : LedgerPayoutScheme
{
    /// <summary>Bounds memory on pools too small to ever fill the window.</summary>
    public const int MaxBuckets = 1440;

    private static readonly TimeSpan BucketLength = TimeSpan.FromMinutes(1);

    protected override void PrepareForShare(ShareLedger ledger, double networkDifficulty)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        var now = timeProvider.GetUtcNow();
        if (ledger.NewestBucketStart is { } newest && now - newest < BucketLength)
        {
            return;
        }

        // History only changes shape when a bucket is opened, so that is the one place it is tidied.
        if (networkDifficulty > 0)
        {
            ledger.TrimTo(windowMultiplier * networkDifficulty);
        }

        ledger.CompactBelow(MaxBuckets);
        ledger.StartBucket(now);
    }

    protected override void OnBlockFound(ShareLedger ledger)
    {
        // The window slides on regardless of blocks.
    }
}