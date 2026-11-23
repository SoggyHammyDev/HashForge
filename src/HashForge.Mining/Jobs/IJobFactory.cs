using HashForge.Bitcoin.Rpc;
using HashForge.Domain;

namespace HashForge.Mining.Jobs;

public interface IJobFactory
{
    Task<MiningJob> CreateAsync(string jobId, PoolDefinition pool, BlockTemplate template, string minerAddress, CancellationToken cancellationToken);
}
