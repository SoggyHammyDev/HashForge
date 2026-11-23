using HashForge.Domain;

namespace HashForge.Mining.Pools;

public interface IPoolProvisioner
{
    Task<ProvisionResult> AddAsync(PoolDefinition pool, CancellationToken cancellationToken);

    Task<bool> RemoveAsync(string name, CancellationToken cancellationToken);
}
