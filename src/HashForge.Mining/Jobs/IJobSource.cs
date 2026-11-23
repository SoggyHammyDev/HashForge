using HashForge.Domain;

namespace HashForge.Mining.Jobs;

public interface IJobSource
{
    event EventHandler<JobsChangedEventArgs>? JobsChanged;

    /// <summary>The job a miner should work on now, or null while the node has not supplied a template.</summary>
    Task<MiningJob?> GetCurrentJobAsync(PoolDefinition pool, string minerAddress, CancellationToken cancellationToken);

    /// <summary>Finds a job that is still valid for the current chain tip.</summary>
    MiningJob? Find(string jobId);
}
