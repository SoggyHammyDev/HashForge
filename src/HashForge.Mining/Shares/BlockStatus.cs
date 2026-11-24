namespace HashForge.Mining.Shares;

/// <summary>What became of a block the pool found.</summary>
public enum BlockStatus
{
    /// <summary>On the chain, but its reward cannot be spent yet.</summary>
    Accepted,

    /// <summary>On the chain and deep enough that its reward can be spent.</summary>
    Confirmed,

    /// <summary>The node accepted it, but the network built on a competing block instead. It paid nobody.</summary>
    Orphaned,

    /// <summary>It arrived after the chain had already moved on, so the node never put it on the chain.</summary>
    Stale,

    /// <summary>The node refused it as invalid.</summary>
    Rejected,
}
