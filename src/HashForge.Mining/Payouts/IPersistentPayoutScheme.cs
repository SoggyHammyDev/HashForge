namespace HashForge.Mining.Payouts;

/// <summary>A payout scheme that keeps share history worth preserving across a restart.</summary>
public interface IPersistentPayoutScheme : IPayoutScheme
{
    IReadOnlyList<ShareBucket> ExportState();

    void ImportState(IReadOnlyList<ShareBucket> state);
}
