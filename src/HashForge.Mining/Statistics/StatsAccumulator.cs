using HashForge.Domain;

namespace HashForge.Mining.Statistics;

/// <summary>
/// Share counters, round effort, best share and hashrate for one subject.
/// The same class serves workers, miners and pools.
/// </summary>
public sealed class StatsAccumulator(TimeSpan hashrateWindow)
{
    private readonly Lock _gate = new();
    private readonly SlidingWindowSum _recentDifficulty = new(hashrateWindow);
    private Dictionary<ShareOutcome, long> _rejected = [];
    private long _accepted;
    private double _roundDifficulty;
    private BestShare? _bestShare;
    private DateTimeOffset? _lastShareAt;

    /// <summary>Records a share and returns true when it is this subject's best so far.</summary>
    public bool Record(ShareResult share)
    {
        ArgumentNullException.ThrowIfNull(share);

        lock (_gate)
        {
            _lastShareAt = share.Timestamp;
            if (!share.IsAccepted)
            {
                _rejected[share.Outcome] = _rejected.GetValueOrDefault(share.Outcome) + 1;
                return false;
            }

            _accepted++;
            _roundDifficulty += share.CreditedDifficulty;
            _recentDifficulty.Add(share.CreditedDifficulty, share.Timestamp);
            if (_bestShare is not null && share.ActualDifficulty <= _bestShare.Difficulty)
            {
                return false;
            }

            _bestShare = new BestShare(share.ActualDifficulty, share.Worker.FullName, share.Timestamp);
            return true;
        }
    }

    /// <summary>True when a share, accepted or not, has arrived since <paramref name="cutoff"/>.</summary>
    public bool IsActiveSince(DateTimeOffset cutoff)
    {
        lock (_gate)
        {
            return _lastShareAt >= cutoff;
        }
    }

    /// <summary>Starts counting effort afresh, as happens when a block is found.</summary>
    public void ResetRound()
    {
        lock (_gate)
        {
            _roundDifficulty = 0;
        }
    }

    public StatsSnapshot Snapshot(DateTimeOffset now, double networkDifficulty)
    {
        lock (_gate)
        {
            return new StatsSnapshot(
                _accepted,
                _rejected.Values.Sum(),
                new Dictionary<ShareOutcome, long>(_rejected),
                _roundDifficulty,
                WorkMath.EffortPercent(_roundDifficulty, networkDifficulty),
                WorkMath.Hashrate(_recentDifficulty.Sum(now), hashrateWindow),
                _bestShare,
                _lastShareAt);
        }
    }

    public AccumulatorState Export()
    {
        lock (_gate)
        {
            return new AccumulatorState(_accepted, new Dictionary<ShareOutcome, long>(_rejected), _roundDifficulty, _bestShare, _lastShareAt);
        }
    }

    public void Import(AccumulatorState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_gate)
        {
            _accepted = state.Accepted;
            _rejected = new Dictionary<ShareOutcome, long>(state.Rejected);
            _roundDifficulty = state.RoundDifficulty;
            _bestShare = state.BestShare;
            _lastShareAt = state.LastShareAt;
        }
    }
}
