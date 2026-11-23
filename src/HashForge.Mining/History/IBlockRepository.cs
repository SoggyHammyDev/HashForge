using HashForge.Mining.Shares;

namespace HashForge.Mining.History;

/// <summary>Durable archive of the blocks the pool has found.</summary>
public interface IBlockRepository
{
    Task AddAsync(FoundBlock block, CancellationToken cancellationToken);

    Task<IReadOnlyList<FoundBlock>> GetRecentAsync(int count, CancellationToken cancellationToken);
}
