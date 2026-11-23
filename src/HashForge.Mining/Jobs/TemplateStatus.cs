namespace HashForge.Mining.Jobs;

/// <summary>What the pool last heard from the node about work.</summary>
/// <param name="Tip">The hash of the block current work builds on, or null before the first template.</param>
public sealed record TemplateStatus(string? Tip, DateTimeOffset RefreshedAt)
{
    /// <summary>What the block being mined would pay in total: the subsidy plus the fees of its transactions.</summary>
    public long? RewardSatoshis { get; init; }
}