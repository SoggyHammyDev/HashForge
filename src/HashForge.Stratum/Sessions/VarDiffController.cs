namespace HashForge.Stratum.Sessions;

/// <summary>
/// Adjusts one connection's share difficulty so shares arrive at a steady, configured rate
/// whatever the miner's hashrate.
/// </summary>
public sealed class VarDiffController(StratumOptions options, DateTimeOffset startedAt)
{
    private const double SecondsPerMinute = 60;
    private const double MaxStepFactor = 4;
    private const double PercentScale = 100;

    /// <summary>A flood of shares triggers an early retarget once it reaches this multiple of a normal window.</summary>
    private const double EarlyRetargetFactor = 2;

    private readonly Lock _gate = new();
    private DateTimeOffset _windowStart = startedAt;
    private int _sharesInWindow;
    private double _difficulty = options.InitialDifficulty;
    private double? _previousDifficulty;

    public double Difficulty
    {
        get
        {
            lock (_gate)
            {
                return _difficulty;
            }
        }
    }

    /// <summary>The difficulty before the latest change; work issued under it may still be in flight.</summary>
    public double? PreviousDifficulty
    {
        get
        {
            lock (_gate)
            {
                return _previousDifficulty;
            }
        }
    }

    public void RecordShare()
    {
        lock (_gate)
        {
            _sharesInWindow++;
        }
    }

    /// <summary>Applies a difficulty requested by the miner, kept within the configured limits.</summary>
    public double SetDifficulty(double requested, DateTimeOffset now)
    {
        lock (_gate)
        {
            Apply(Math.Clamp(requested, options.MinDifficulty, options.MaxDifficulty), now);
            return _difficulty;
        }
    }

    /// <summary>Returns the new difficulty when a change is due, otherwise null.</summary>
    public double? TryRetarget(DateTimeOffset now)
    {
        lock (_gate)
        {
            var elapsedSeconds = (now - _windowStart).TotalSeconds;
            var expectedShares = options.TargetSharesPerMinute * options.RetargetSeconds / SecondsPerMinute;
            var isFlooding = _sharesInWindow >= expectedShares * EarlyRetargetFactor;
            if (elapsedSeconds <= 0 || (elapsedSeconds < options.RetargetSeconds && !isFlooding))
            {
                return null;
            }

            var ratio = _sharesInWindow / (elapsedSeconds / SecondsPerMinute) / options.TargetSharesPerMinute;
            var tolerance = options.VariancePercent / PercentScale;
            var step = Math.Clamp(ratio, 1 / MaxStepFactor, MaxStepFactor);
            var target = Math.Clamp(_difficulty * step, options.MinDifficulty, options.MaxDifficulty);
            if (Math.Abs(ratio - 1) <= tolerance || target == _difficulty)
            {
                StartWindow(now);
                return null;
            }

            Apply(target, now);
            return target;
        }
    }

    private void Apply(double difficulty, DateTimeOffset now)
    {
        if (difficulty != _difficulty)
        {
            _previousDifficulty = _difficulty;
            _difficulty = difficulty;
        }

        StartWindow(now);
    }

    private void StartWindow(DateTimeOffset now)
    {
        _windowStart = now;
        _sharesInWindow = 0;
    }
}
