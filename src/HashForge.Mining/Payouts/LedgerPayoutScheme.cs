namespace HashForge.Mining.Payouts;

/// <summary>
/// Base for schemes that split a block by recorded work. Subclasses decide only how the history
/// is shaped as shares arrive and what happens to it when a block is found.
/// </summary>
public abstract class LedgerPayoutScheme : IPersistentPayoutScheme
{
    private readonly Lock _gate = new();
    private readonly ShareLedger _ledger = new();

    /// <summary>
    /// With no history yet (a new pool, or the moment after a round ends) there is nobody to split with,
    /// so a block found then is paid to its finder rather than to nobody.
    /// </summary>
    public bool IsMinerSpecific
    {
        get
        {
            lock (_gate)
            {
                return _ledger.IsEmpty;
            }
        }
    }

    public PayoutPlan GetPlan(string minerAddress)
    {
        lock (_gate)
        {
            return new PayoutPlan(_ledger.IsEmpty
                ? new Dictionary<string, double>(StringComparer.Ordinal) { [minerAddress] = 1d }
                : _ledger.Totals());
        }
    }

    public void RecordShare(string address, double difficulty, double networkDifficulty)
    {
        lock (_gate)
        {
            PrepareForShare(_ledger, networkDifficulty);
            _ledger.Add(address, difficulty);
        }
    }

    public void OnBlockFound()
    {
        lock (_gate)
        {
            OnBlockFound(_ledger);
        }
    }

    public IReadOnlyList<ShareBucket> ExportState()
    {
        lock (_gate)
        {
            return _ledger.Export();
        }
    }

    public void ImportState(IReadOnlyList<ShareBucket> state)
    {
        lock (_gate)
        {
            _ledger.Import(state);
        }
    }

    /// <summary>Makes sure the ledger has a bucket ready for the next share, tidying old history if needed.</summary>
    protected abstract void PrepareForShare(ShareLedger ledger, double networkDifficulty);

    protected abstract void OnBlockFound(ShareLedger ledger);
}