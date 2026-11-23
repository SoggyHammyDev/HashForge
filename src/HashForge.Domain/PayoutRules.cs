namespace HashForge.Domain;

/// <summary>Limits applied when splitting a block reward into coinbase outputs.</summary>
public sealed record PayoutRules(decimal FeePercent, string FeeAddress, int MaxOutputs, long MinimumPayoutSatoshis);
