namespace HashForge.Mining.Payouts;

/// <summary>
/// Share history as time-ordered buckets of per-miner totals, never individual shares.
/// Not thread-safe; the payout scheme that owns it does the locking.
/// </summary>
public sealed class ShareLedger
{
    /// <summary>Below this, a miner's remaining total is floating-point residue rather than real work.</summary>
    private const double Residue = 1e-9;

    private readonly LinkedList<ShareBucket> _buckets = new();
    private readonly Dictionary<string, double> _totals = new(StringComparer.Ordinal);

    public bool IsEmpty => _totals.Count == 0;

    public int BucketCount => _buckets.Count;

    public DateTimeOffset? NewestBucketStart => _buckets.Last?.Value.Start;

    public void StartBucket(DateTimeOffset start) =>
        _buckets.AddLast(new ShareBucket(start, new Dictionary<string, double>(StringComparer.Ordinal)));

    /// <summary>Adds work to the newest bucket, which must exist.</summary>
    public void Add(string address, double difficulty)
    {
        Increase(_buckets.Last!.Value.Shares, address, difficulty);
        Increase(_totals, address, difficulty);
    }

    public Dictionary<string, double> Totals() => new(_totals, StringComparer.Ordinal);

    public void Clear()
    {
        _buckets.Clear();
        _totals.Clear();
    }

    /// <summary>Drops the oldest buckets while the remaining ones still hold at least <paramref name="windowSize"/> of work.</summary>
    public void TrimTo(double windowSize)
    {
        var total = _totals.Values.Sum();
        while (_buckets.First is { } oldest && total - oldest.Value.Shares.Values.Sum() >= windowSize)
        {
            _buckets.RemoveFirst();
            foreach (var (address, difficulty) in oldest.Value.Shares)
            {
                Decrease(address, difficulty);
            }

            total = _totals.Values.Sum();
        }
    }

    /// <summary>Merges the oldest buckets together until fewer than <paramref name="maxBuckets"/> remain. No work is lost.</summary>
    public void CompactBelow(int maxBuckets)
    {
        while (_buckets.Count >= maxBuckets && _buckets.Count > 1)
        {
            var oldest = _buckets.First!.Value;
            _buckets.RemoveFirst();
            foreach (var (address, difficulty) in _buckets.First!.Value.Shares)
            {
                Increase(oldest.Shares, address, difficulty);
            }

            _buckets.First.Value = oldest;
        }
    }

    public IReadOnlyList<ShareBucket> Export() =>
        [.. _buckets.Select(b => new ShareBucket(b.Start, new Dictionary<string, double>(b.Shares)))];

    public void Import(IReadOnlyList<ShareBucket> state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Clear();
        foreach (var bucket in state.OrderBy(b => b.Start))
        {
            StartBucket(bucket.Start);
            foreach (var (address, difficulty) in bucket.Shares)
            {
                Add(address, difficulty);
            }
        }
    }

    private static void Increase(Dictionary<string, double> target, string address, double difficulty) =>
        target[address] = target.GetValueOrDefault(address) + difficulty;

    private void Decrease(string address, double difficulty)
    {
        var remaining = _totals[address] - difficulty;
        if (remaining <= Residue * difficulty)
        {
            _totals.Remove(address);
        }
        else
        {
            _totals[address] = remaining;
        }
    }
}
