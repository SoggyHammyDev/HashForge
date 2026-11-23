namespace HashForge.Mining.Payouts;

/// <summary>PROP: the reward is split by the work done in the current round, which ends when a block is found.</summary>
public sealed class ProportionalPayoutScheme(TimeProvider timeProvider) : LedgerPayoutScheme
{
    protected override void PrepareForShare(ShareLedger ledger, double networkDifficulty)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        // A round is one undivided total, so a single bucket is enough.
        if (ledger.BucketCount == 0)
        {
            ledger.StartBucket(timeProvider.GetUtcNow());
        }
    }

    protected override void OnBlockFound(ShareLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ledger.Clear();
    }
}