namespace HashForge.Domain;

/// <summary>Limits applied when splitting a block reward into coinbase outputs.</summary>
public sealed record PayoutRules(decimal FeePercent, string FeeAddress, int MaxOutputs, long MinimumPayoutSatoshis)
{
    /// <summary>Lowers the output limit to the given ceiling; zero or less means there is no ceiling.</summary>
    public PayoutRules CappedAt(int outputCeiling) =>
        outputCeiling > 0 && outputCeiling < MaxOutputs ? this with { MaxOutputs = outputCeiling } : this;
}
