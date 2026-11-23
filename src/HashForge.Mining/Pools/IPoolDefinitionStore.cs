using HashForge.Domain;

namespace HashForge.Mining.Pools;

/// <summary>Stores the pools an administrator created at runtime so they survive a restart.</summary>
public interface IPoolDefinitionStore
{
    Task<IReadOnlyList<PoolDefinition>> GetAllAsync(CancellationToken cancellationToken);

    Task SaveAsync(PoolDefinition pool, CancellationToken cancellationToken);

    Task DeleteAsync(string name, CancellationToken cancellationToken);
}
