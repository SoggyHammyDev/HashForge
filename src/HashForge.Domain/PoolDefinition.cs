namespace HashForge.Domain;

/// <summary>One pool: a Stratum port bound to a payout scheme.</summary>
public sealed record PoolDefinition
{
    public const int DefaultMaxCoinbaseOutputs = 500;
    public const long DefaultMinimumPayoutSatoshis = 10_000;
    public const double DefaultPplnsWindowMultiplier = 2d;
    public const long DefaultFinderBonusSatoshis = 100_000_000;

    public required string Name { get; init; }

    public required PayoutScheme Scheme { get; init; }

    public required int Port { get; init; }

    public bool Enabled { get; init; } = true;

    public decimal FeePercent { get; init; }

    /// <summary>Receives the pool fee, and nothing else. May be left empty when the fee is zero.</summary>
    public string FeeAddress { get; init; } = string.Empty;

    public bool HasFee => FeePercent > 0;

    /// <summary>The PPLNS window size as a multiple of network difficulty.</summary>
    public double PplnsWindowMultiplier { get; init; } = DefaultPplnsWindowMultiplier;

    /// <summary>What the block finder receives before the rest is split. Only used by the finder-bonus scheme.</summary>
    public long FinderBonusSatoshis { get; init; } = DefaultFinderBonusSatoshis;

    public int MaxCoinbaseOutputs { get; init; } = DefaultMaxCoinbaseOutputs;

    public long MinimumPayoutSatoshis { get; init; } = DefaultMinimumPayoutSatoshis;

    public PayoutRules ToPayoutRules() => new(FeePercent, FeeAddress, MaxCoinbaseOutputs, MinimumPayoutSatoshis);
}
