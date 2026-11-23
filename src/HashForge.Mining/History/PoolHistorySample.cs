namespace HashForge.Mining.History;

/// <summary>One reading of a pool's size, kept so the UI can chart it over time.</summary>
public sealed record PoolHistorySample(string PoolName, DateTimeOffset Timestamp, double Hashrate, int Miners, int Workers);
