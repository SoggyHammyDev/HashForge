using HashForge.Domain;

namespace HashForge.Mining.Shares;

/// <summary>The outcome of checking a share's proof of work.</summary>
/// <param name="Block">Set when the share also satisfies the network target.</param>
public sealed record ShareValidation(
    ShareOutcome Outcome,
    double CreditedDifficulty,
    double ActualDifficulty,
    BlockCandidate? Block = null)
{
    public static ShareValidation Rejected(ShareOutcome outcome, double actualDifficulty = 0) =>
        new(outcome, 0, actualDifficulty);
}
