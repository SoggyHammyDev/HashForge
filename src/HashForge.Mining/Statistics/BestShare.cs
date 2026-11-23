namespace HashForge.Mining.Statistics;

/// <summary>The highest-difficulty share seen, and which worker submitted it.</summary>
public sealed record BestShare(double Difficulty, string Worker, DateTimeOffset Timestamp);
