namespace HashForge.Mining.Payouts;

/// <summary>Decides how much of a block each miner has earned.</summary>
public interface IPayoutScheme
{
    /// <summary>True when the plan depends on which miner is asking, so each miner needs their own coinbase.</summary>
    bool IsMinerSpecific { get; }

    /// <summary>The payout plan for a block mined on work handed to <paramref name="minerAddress"/>.</summary>
    PayoutPlan GetPlan(string minerAddress);

    void RecordShare(string address, double difficulty, double networkDifficulty);

    void OnBlockFound();
}
