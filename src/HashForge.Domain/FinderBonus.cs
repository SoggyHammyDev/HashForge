namespace HashForge.Domain;

/// <summary>A fixed amount paid to the miner who finds the block, before the rest is split.</summary>
public sealed record FinderBonus(string Address, long Satoshis);
