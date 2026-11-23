namespace HashForge.Domain;

/// <summary>The verdict on one submitted share.</summary>
/// <param name="CreditedDifficulty">The difficulty the share counts for (the worker's assigned difficulty).</param>
/// <param name="ActualDifficulty">The difficulty the share's hash actually achieved.</param>
public sealed record ShareResult(
    string PoolName,
    WorkerIdentity Worker,
    ShareOutcome Outcome,
    double CreditedDifficulty,
    double ActualDifficulty,
    DateTimeOffset Timestamp)
{
    public bool IsAccepted => Outcome == ShareOutcome.Accepted;
}
