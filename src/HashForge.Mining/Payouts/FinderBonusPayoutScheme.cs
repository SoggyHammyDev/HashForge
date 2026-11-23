using HashForge.Domain;

namespace HashForge.Mining.Payouts;

/// <summary>
/// Adds a fixed bonus for the block finder on top of another scheme's split. Every miner needs their own
/// coinbase, because it names them as the bonus recipient.
/// </summary>
public sealed class FinderBonusPayoutScheme(IPersistentPayoutScheme inner, long bonusSatoshis) : IPersistentPayoutScheme
{
    public bool IsMinerSpecific => true;

    public PayoutPlan GetPlan(string minerAddress) =>
        inner.GetPlan(minerAddress) with { FinderBonus = new FinderBonus(minerAddress, bonusSatoshis) };

    public void RecordShare(string address, double difficulty, double networkDifficulty) =>
        inner.RecordShare(address, difficulty, networkDifficulty);

    public void OnBlockFound() => inner.OnBlockFound();

    public IReadOnlyList<ShareBucket> ExportState() => inner.ExportState();

    public void ImportState(IReadOnlyList<ShareBucket> state) => inner.ImportState(state);
}