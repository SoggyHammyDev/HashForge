using HashForge.Domain;

namespace HashForge.Mining.Payouts;

/// <summary>Who should be paid for a block, and in what proportions.</summary>
/// <param name="Weights">Relative shares of the reward by address.</param>
/// <param name="FinderBonus">A fixed amount for the block finder, taken before the split, when the scheme has one.</param>
public sealed record PayoutPlan(IReadOnlyDictionary<string, double> Weights, FinderBonus? FinderBonus = null);
