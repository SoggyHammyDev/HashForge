namespace HashForge.Mining.Shares;

/// <summary>A solved block: its hash and its full serialisation, ready to submit.</summary>
public sealed record BlockCandidate(string Hash, string Hex);
