using HashForge.Mining.Shares;

namespace HashForge.Mining.History;

/// <summary>Durable archive of the blocks the pool has found.</summary>
public interface IBlockRepository
{
    Task AddAsync(FoundBlock block, CancellationToken cancellationToken);

    Task<IReadOnlyList<FoundBlock>> GetRecentAsync(int count, CancellationToken cancellationToken);

    /// <summary>The most recent blocks one pool found, newest first.</summary>
    Task<IReadOnlyList<FoundBlock>> GetRecentForPoolAsync(string poolName, int count, CancellationToken cancellationToken);

    /// <summary>The block with this hash, or null when no pool here found it.</summary>
    Task<FoundBlock?> FindAsync(string hash, CancellationToken cancellationToken);
}
