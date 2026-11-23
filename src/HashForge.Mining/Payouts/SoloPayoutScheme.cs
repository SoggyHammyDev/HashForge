namespace HashForge.Mining.Payouts;

/// <summary>SOLO: the miner who finds the block takes the reward, so there is no history to keep.</summary>
public sealed class SoloPayoutScheme : IPayoutScheme
{
    public bool IsMinerSpecific => true;

    public PayoutPlan GetPlan(string minerAddress) =>
        new(new Dictionary<string, double>(StringComparer.Ordinal) { [minerAddress] = 1d });

    public void RecordShare(string address, double difficulty, double networkDifficulty)
    {
        // Other miners' work has no bearing on a solo payout.
    }

    public void OnBlockFound()
    {
        // Nothing to reset.
    }
}