namespace HashForge.Mining.Payouts;

/// <summary>Shares grouped by miner address for one slice of time.</summary>
public sealed record ShareBucket(DateTimeOffset Start, Dictionary<string, double> Shares);
